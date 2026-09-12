using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Storage;

namespace KeyKeeper.Core.Auth;

public sealed class AuthenticationService : IAuthenticationService, IDisposable
{
    private readonly IKeyDerivationService _kdf;
    private readonly IMasterPasswordVerifier _verifier;
    private readonly IEncryptionService _encryption;
    private readonly IVaultDatabaseService _vaultDatabase;
    private readonly IVaultUnlockStore _unlockStore;
    private readonly IWrappedKeyStorage _wrappedKeyStorage;
    private readonly IBiometricAuthService _biometricAuth;
    private readonly IInactivityTimerService _inactivityTimer;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    private byte[]? _currentVaultKey;

    public AuthenticationService(
        IKeyDerivationService kdf,
        IMasterPasswordVerifier verifier,
        IEncryptionService encryption,
        IVaultDatabaseService vaultDatabase,
        IVaultUnlockStore unlockStore,
        IWrappedKeyStorage wrappedKeyStorage,
        IBiometricAuthService biometricAuth,
        IInactivityTimerService inactivityTimer,
        TimeProvider? timeProvider = null)
    {
        _kdf = kdf ?? throw new ArgumentNullException(nameof(kdf));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _vaultDatabase = vaultDatabase ?? throw new ArgumentNullException(nameof(vaultDatabase));
        _unlockStore = unlockStore ?? throw new ArgumentNullException(nameof(unlockStore));
        _wrappedKeyStorage = wrappedKeyStorage ?? throw new ArgumentNullException(nameof(wrappedKeyStorage));
        _biometricAuth = biometricAuth ?? throw new ArgumentNullException(nameof(biometricAuth));
        _inactivityTimer = inactivityTimer ?? throw new ArgumentNullException(nameof(inactivityTimer));
        _timeProvider = timeProvider ?? TimeProvider.System;

        _inactivityTimer.Timeout = AutoLockTimeout;
        _inactivityTimer.TimedOut += OnInactivityTimedOut;
    }

    public bool IsVaultInitialized => _unlockStore.IsInitialized;

    public bool IsUnlocked { get; private set; }

    public event Action? Locked;

    public TimeSpan AutoLockTimeout
    {
        get;
        set
        {
            field = value;
            _inactivityTimer.Timeout = value;
        }
    } = TimeSpan.FromMinutes(2);

    public Task<AuthenticationResult> InitializeVaultAsync(char[] masterPassword)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        if (IsVaultInitialized)
        {
            throw new InvalidOperationException("The vault has already been initialized.");
        }

        return Task.Run(() =>
        {
            byte[] salt = _kdf.GenerateSalt();
            Argon2Parameters kdfParams = Argon2Parameters.Default;
            byte[] key = _kdf.DeriveKey(masterPassword, salt, kdfParams);
            byte[] masterVerifier = _verifier.CreateVerifier(key);

            _unlockStore.WriteMetadata(new VaultUnlockMetadata(
                SchemaVersion: 1,
                Salt: salt,
                Argon2MemorySizeKiB: kdfParams.MemorySizeKiB,
                Argon2Iterations: kdfParams.Iterations,
                Argon2Parallelism: kdfParams.Parallelism,
                Argon2OutputLengthBytes: kdfParams.OutputLengthBytes,
                MasterPasswordVerifier: masterVerifier,
                CreatedAtUtc: _timeProvider.GetUtcNow()));
            _unlockStore.WriteLockoutState(LockoutState.None);

            _vaultDatabase.Open(key);
            AdoptUnlockedKey(key);

            return AuthenticationResult.Success;
        });
    }

    public Task<AuthenticationResult> UnlockWithMasterPasswordAsync(char[] masterPassword)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        if (!IsVaultInitialized)
        {
            throw new InvalidOperationException("The vault has not been initialized yet.");
        }

        return Task.Run(() =>
        {
            AuthenticationResult? lockedOut = CheckLockout();
            if (lockedOut is not null)
            {
                return lockedOut;
            }

            VaultUnlockMetadata metadata = _unlockStore.ReadMetadata()!;
            var kdfParams = new Argon2Parameters(
                metadata.Argon2MemorySizeKiB,
                metadata.Argon2Iterations,
                metadata.Argon2Parallelism,
                metadata.Argon2OutputLengthBytes);

            byte[] key = _kdf.DeriveKey(masterPassword, metadata.Salt, kdfParams);

            if (!_verifier.Verify(key, metadata.MasterPasswordVerifier))
            {
                SecureMemoryHelper.Clear(key);
                return RecordFailedAttempt();
            }

            ResetLockout();

            try
            {
                _vaultDatabase.Open(key);
            }
            catch (VaultDatabaseException)
            {
                // The verifier matched but the database itself won't open -- something beyond a
                // wrong password (corruption, tampering). Don't mask this as WrongPassword.
                SecureMemoryHelper.Clear(key);
                throw;
            }

            AdoptUnlockedKey(key);
            return AuthenticationResult.Success;
        });
    }

    public async Task<AuthenticationResult> TryUnlockWithBiometricsAsync()
    {
        byte[]? wrappedKey = await _wrappedKeyStorage.RetrieveAsync().ConfigureAwait(false);
        if (wrappedKey is null)
        {
            return new AuthenticationResult(AuthenticationResultKind.BiometricUnavailable, 0, null);
        }

        BiometricAvailability availability = await _biometricAuth.CheckAvailabilityAsync().ConfigureAwait(false);
        if (availability != BiometricAvailability.Available)
        {
            SecureMemoryHelper.Clear(wrappedKey);
            return new AuthenticationResult(AuthenticationResultKind.BiometricUnavailable, 0, null);
        }

        BiometricAuthOutcome outcome = await _biometricAuth
            .AuthenticateAsync("Kasayı açmak için kimliğinizi doğrulayın")
            .ConfigureAwait(false);

        if (outcome != BiometricAuthOutcome.Success)
        {
            SecureMemoryHelper.Clear(wrappedKey);
            return new AuthenticationResult(AuthenticationResultKind.BiometricFailed, 0, null);
        }

        return await Task.Run(() =>
        {
            try
            {
                _vaultDatabase.Open(wrappedKey);
            }
            catch (VaultDatabaseException)
            {
                SecureMemoryHelper.Clear(wrappedKey);
                return new AuthenticationResult(AuthenticationResultKind.BiometricFailed, 0, null);
            }

            AdoptUnlockedKey(wrappedKey);
            return AuthenticationResult.Success;
        }).ConfigureAwait(false);
    }

    public Task EnableBiometricUnlockAsync()
    {
        byte[]? key;
        lock (_gate)
        {
            if (!IsUnlocked || _currentVaultKey is null)
            {
                throw new InvalidOperationException("The vault must be unlocked before enabling biometric unlock.");
            }

            key = (byte[])_currentVaultKey.Clone();
        }

        return _wrappedKeyStorage.StoreAsync(key);
    }

    public void Lock()
    {
        bool wasUnlocked;
        lock (_gate)
        {
            wasUnlocked = IsUnlocked;
            _inactivityTimer.Stop();
            SecureMemoryHelper.Clear(_currentVaultKey);
            _currentVaultKey = null;
            _vaultDatabase.Close();
            IsUnlocked = false;
        }

        // Raised outside the lock: subscribers (UI navigation) shouldn't run while _gate is
        // held, and Lock() must stay safely reentrant if a handler itself calls back in here.
        if (wasUnlocked)
        {
            Locked?.Invoke();
        }
    }

    public void NotifyUserActivity() => _inactivityTimer.Reset();

    public byte[] EncryptWithVaultKey(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        lock (_gate)
        {
            if (_currentVaultKey is null)
            {
                throw new InvalidOperationException("The vault is locked.");
            }

            return _encryption.Encrypt(plaintext, _currentVaultKey);
        }
    }

    public byte[] DecryptWithVaultKey(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        lock (_gate)
        {
            if (_currentVaultKey is null)
            {
                throw new InvalidOperationException("The vault is locked.");
            }

            return _encryption.Decrypt(ciphertext, _currentVaultKey);
        }
    }

    public void Dispose()
    {
        _inactivityTimer.TimedOut -= OnInactivityTimedOut;
        Lock();
    }

    private void AdoptUnlockedKey(byte[] key)
    {
        lock (_gate)
        {
            _currentVaultKey = key;
            IsUnlocked = true;
        }

        _inactivityTimer.Start();
    }

    private void OnInactivityTimedOut() => Lock();

    private AuthenticationResult? CheckLockout()
    {
        LockoutState state = _unlockStore.ReadLockoutState();
        if (state.LockedUntilUtc is { } until && until > _timeProvider.GetUtcNow())
        {
            return new AuthenticationResult(AuthenticationResultKind.LockedOut, state.FailedAttemptCount, until - _timeProvider.GetUtcNow());
        }

        return null;
    }

    private AuthenticationResult RecordFailedAttempt()
    {
        LockoutState current = _unlockStore.ReadLockoutState();
        int newCount = current.FailedAttemptCount + 1;
        TimeSpan? duration = LockoutPolicy.GetLockoutDuration(newCount);
        DateTimeOffset? lockedUntil = duration is { } d ? _timeProvider.GetUtcNow() + d : current.LockedUntilUtc;

        _unlockStore.WriteLockoutState(new LockoutState(newCount, lockedUntil));

        TimeSpan? remaining = lockedUntil is { } u && u > _timeProvider.GetUtcNow() ? u - _timeProvider.GetUtcNow() : null;
        return new AuthenticationResult(AuthenticationResultKind.WrongPassword, newCount, remaining);
    }

    private void ResetLockout() => _unlockStore.WriteLockoutState(LockoutState.None);
}
