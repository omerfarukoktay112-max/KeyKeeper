using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Storage;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace KeyKeeper.Core.Tests.Auth;

public class AuthenticationServiceTests
{
    /// <summary>In-memory <see cref="IVaultUnlockStore"/> -- no real database file, since these
    /// tests are about <see cref="AuthenticationService"/>'s orchestration logic, not storage
    /// (that's <c>VaultUnlockStoreTests</c>'s job).</summary>
    private sealed class FakeVaultUnlockStore : IVaultUnlockStore
    {
        private VaultUnlockMetadata? _metadata;
        private LockoutState _lockout = LockoutState.None;

        public bool IsInitialized => _metadata is not null;
        public VaultUnlockMetadata? ReadMetadata() => _metadata;
        public void WriteMetadata(VaultUnlockMetadata metadata) => _metadata = metadata;
        public LockoutState ReadLockoutState() => _lockout;
        public void WriteLockoutState(LockoutState state) => _lockout = state;
    }

    /// <summary>Records what it was asked to do instead of touching a real SQLCipher database --
    /// <see cref="VaultDatabaseServiceTests"/> already covers the real thing.</summary>
    private sealed class FakeVaultDatabaseService : IVaultDatabaseService
    {
        public int OpenCallCount { get; private set; }
        public byte[]? LastOpenedKey { get; private set; }
        public Exception? ThrowOnOpen { get; set; }

        public bool IsOpen { get; private set; }

        public SQLiteConnection Open(byte[] key)
        {
            OpenCallCount++;
            if (ThrowOnOpen is { } exception)
            {
                throw exception;
            }

            LastOpenedKey = key;
            IsOpen = true;
            return null!;
        }

        public void Close() => IsOpen = false;

        public SQLiteConnection GetOpenConnectionOrThrow() =>
            IsOpen ? null! : throw new InvalidOperationException("The vault database is not open.");

        public void Dispose() => Close();
    }

    private sealed class FakeBiometricAuthService : IBiometricAuthService
    {
        public BiometricAvailability Availability { get; set; } = BiometricAvailability.Available;
        public BiometricAuthOutcome Outcome { get; set; } = BiometricAuthOutcome.Success;

        public Task<BiometricAvailability> CheckAvailabilityAsync() => Task.FromResult(Availability);

        public Task<BiometricAuthOutcome> AuthenticateAsync(string reason) => Task.FromResult(Outcome);
    }

    /// <summary>Stands in for the OS-backed secure store, same as <c>WrappedKeyStorageTests</c>.</summary>
    private sealed class InMemorySecureKeyValueStore : ISecureKeyValueStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.TryGetValue(key, out string? value) ? value : null);

        public void Remove(string key) => _values.Remove(key);
    }

    private sealed record Sut(
        AuthenticationService Service,
        FakeVaultUnlockStore UnlockStore,
        FakeVaultDatabaseService Database,
        IWrappedKeyStorage WrappedKeyStorage,
        FakeBiometricAuthService Biometrics,
        FakeTimeProvider Time);

    private static Sut CreateSut()
    {
        var unlockStore = new FakeVaultUnlockStore();
        var database = new FakeVaultDatabaseService();
        var wrappedKeyStorage = new WrappedKeyStorage(new InMemorySecureKeyValueStore());
        var biometrics = new FakeBiometricAuthService();
        var time = new FakeTimeProvider();
        var inactivityTimer = new InactivityTimerService(time);

        var service = new AuthenticationService(
            new Argon2idKeyDerivationService(),
            new MasterPasswordVerifier(),
            new AesGcmEncryptionService(),
            database,
            unlockStore,
            wrappedKeyStorage,
            biometrics,
            inactivityTimer,
            time);

        return new Sut(service, unlockStore, database, wrappedKeyStorage, biometrics, time);
    }

    private static char[] Password(string value) => value.ToCharArray();

    [Fact]
    public async Task InitializeVaultAsync_FirstCall_ReturnsSuccessAndUnlocksTheVault()
    {
        Sut sut = CreateSut();

        AuthenticationResult result = await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));

        Assert.Equal(AuthenticationResult.Success, result);
        Assert.True(sut.Service.IsVaultInitialized);
        Assert.True(sut.Service.IsUnlocked);
        Assert.Equal(1, sut.Database.OpenCallCount);
        Assert.Equal(32, sut.Database.LastOpenedKey?.Length);
    }

    [Fact]
    public async Task InitializeVaultAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Service.InitializeVaultAsync(Password("another password")));
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_VaultNotInitialized_ThrowsInvalidOperationException()
    {
        Sut sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Service.UnlockWithMasterPasswordAsync(Password("whatever")));
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_CorrectPassword_ReturnsSuccessAndReopensTheDatabase()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        sut.Service.Lock();

        AuthenticationResult result = await sut.Service.UnlockWithMasterPasswordAsync(Password("correct horse battery staple"));

        Assert.Equal(AuthenticationResult.Success, result);
        Assert.True(sut.Service.IsUnlocked);
        Assert.Equal(2, sut.Database.OpenCallCount);
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_WrongPassword_ReturnsWrongPasswordAndDoesNotOpenTheDatabase()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        sut.Service.Lock();

        AuthenticationResult result = await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong password"));

        Assert.Equal(AuthenticationResultKind.WrongPassword, result.Kind);
        Assert.Equal(1, result.FailedAttemptCount);
        Assert.False(sut.Service.IsUnlocked);
        Assert.Equal(1, sut.Database.OpenCallCount);
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_EnoughFailuresReachesLockoutTier_LocksOutEvenTheCorrectPassword()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        sut.Service.Lock();

        AuthenticationResult r1 = await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong"));
        AuthenticationResult r2 = await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong"));
        AuthenticationResult r3 = await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong"));

        Assert.Equal(AuthenticationResultKind.WrongPassword, r1.Kind);
        Assert.Equal(1, r1.FailedAttemptCount);
        Assert.Null(r1.RemainingLockout);

        Assert.Equal(AuthenticationResultKind.WrongPassword, r2.Kind);
        Assert.Equal(2, r2.FailedAttemptCount);
        Assert.Null(r2.RemainingLockout);

        // The 3rd failure hits LockoutPolicy's first tier (5s) -- it's still reported as
        // WrongPassword (it's the attempt that caused the lockout), but with a remaining duration.
        Assert.Equal(AuthenticationResultKind.WrongPassword, r3.Kind);
        Assert.Equal(3, r3.FailedAttemptCount);
        Assert.NotNull(r3.RemainingLockout);

        // A 4th attempt, even with the correct password, is rejected without deriving anything.
        AuthenticationResult r4 = await sut.Service.UnlockWithMasterPasswordAsync(Password("correct horse battery staple"));
        Assert.Equal(AuthenticationResultKind.LockedOut, r4.Kind);
        Assert.Equal(3, r4.FailedAttemptCount);
        Assert.False(sut.Service.IsUnlocked);
        Assert.Equal(1, sut.Database.OpenCallCount);
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_AfterLockoutWindowPasses_AcceptsTheCorrectPasswordAgain()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        sut.Service.Lock();

        for (int i = 0; i < 3; i++)
        {
            await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong"));
        }

        sut.Time.Advance(TimeSpan.FromSeconds(6));

        AuthenticationResult result = await sut.Service.UnlockWithMasterPasswordAsync(Password("correct horse battery staple"));

        Assert.Equal(AuthenticationResultKind.Success, result.Kind);
        Assert.True(sut.Service.IsUnlocked);
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_CorrectPasswordAfterPriorFailures_ResetsTheFailedAttemptCounter()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        sut.Service.Lock();

        await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong"));
        await sut.Service.UnlockWithMasterPasswordAsync(Password("correct horse battery staple"));
        sut.Service.Lock();

        AuthenticationResult result = await sut.Service.UnlockWithMasterPasswordAsync(Password("wrong again"));

        Assert.Equal(1, result.FailedAttemptCount);
    }

    [Fact]
    public async Task UnlockWithMasterPasswordAsync_VerifierMatchesButDatabaseFailsToOpen_PropagatesAndStaysLocked()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        sut.Service.Lock();
        sut.Database.ThrowOnOpen = new VaultDatabaseException("simulated corruption");

        await Assert.ThrowsAsync<VaultDatabaseException>(
            () => sut.Service.UnlockWithMasterPasswordAsync(Password("correct horse battery staple")));

        Assert.False(sut.Service.IsUnlocked);
    }

    [Fact]
    public async Task TryUnlockWithBiometricsAsync_NoWrappedKeyStored_ReturnsBiometricUnavailable()
    {
        Sut sut = CreateSut();

        AuthenticationResult result = await sut.Service.TryUnlockWithBiometricsAsync();

        Assert.Equal(AuthenticationResultKind.BiometricUnavailable, result.Kind);
        Assert.False(sut.Service.IsUnlocked);
    }

    [Fact]
    public async Task TryUnlockWithBiometricsAsync_HardwareUnavailable_ReturnsBiometricUnavailable()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        await sut.Service.EnableBiometricUnlockAsync();
        sut.Service.Lock();
        sut.Biometrics.Availability = BiometricAvailability.NotEnrolled;

        AuthenticationResult result = await sut.Service.TryUnlockWithBiometricsAsync();

        Assert.Equal(AuthenticationResultKind.BiometricUnavailable, result.Kind);
        Assert.False(sut.Service.IsUnlocked);
    }

    [Fact]
    public async Task TryUnlockWithBiometricsAsync_UserCancelsThePrompt_ReturnsBiometricFailedAndDoesNotAffectLockout()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        await sut.Service.EnableBiometricUnlockAsync();
        sut.Service.Lock();
        sut.Biometrics.Outcome = BiometricAuthOutcome.Cancelled;

        AuthenticationResult result = await sut.Service.TryUnlockWithBiometricsAsync();

        Assert.Equal(AuthenticationResultKind.BiometricFailed, result.Kind);
        Assert.False(sut.Service.IsUnlocked);
        Assert.Equal(0, sut.UnlockStore.ReadLockoutState().FailedAttemptCount);
    }

    [Fact]
    public async Task TryUnlockWithBiometricsAsync_Success_UnlocksTheVaultUsingTheStoredKey()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        await sut.Service.EnableBiometricUnlockAsync();
        sut.Service.Lock();

        AuthenticationResult result = await sut.Service.TryUnlockWithBiometricsAsync();

        Assert.Equal(AuthenticationResultKind.Success, result.Kind);
        Assert.True(sut.Service.IsUnlocked);
        Assert.Equal(2, sut.Database.OpenCallCount);
    }

    [Fact]
    public async Task EnableBiometricUnlockAsync_VaultIsLocked_ThrowsInvalidOperationException()
    {
        Sut sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Service.EnableBiometricUnlockAsync());
    }

    [Fact]
    public async Task EnableBiometricUnlockAsync_VaultIsUnlocked_StoresARetrievableWrappedKey()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));

        await sut.Service.EnableBiometricUnlockAsync();

        byte[]? stored = await sut.WrappedKeyStorage.RetrieveAsync();
        Assert.NotNull(stored);
        Assert.Equal(32, stored!.Length);
    }

    [Fact]
    public async Task Lock_WhenUnlocked_ClosesTheDatabaseAndMarksTheVaultLocked()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));

        sut.Service.Lock();

        Assert.False(sut.Service.IsUnlocked);
        Assert.False(sut.Database.IsOpen);
    }

    [Fact]
    public void Lock_WhenAlreadyLocked_DoesNotThrow()
    {
        Sut sut = CreateSut();

        Exception? exception = Record.Exception(() => sut.Service.Lock());

        Assert.Null(exception);
    }

    [Fact]
    public async Task Lock_WhenUnlocked_RaisesLockedEvent()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        int raiseCount = 0;
        sut.Service.Locked += () => raiseCount++;

        sut.Service.Lock();

        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void Lock_WhenAlreadyLocked_DoesNotRaiseLockedEvent()
    {
        Sut sut = CreateSut();
        int raiseCount = 0;
        sut.Service.Locked += () => raiseCount++;

        sut.Service.Lock();

        Assert.Equal(0, raiseCount);
    }

    [Fact]
    public async Task InactivityTimeout_AutoLocksTheVault()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));

        sut.Time.Advance(sut.Service.AutoLockTimeout + TimeSpan.FromSeconds(1));

        Assert.False(sut.Service.IsUnlocked);
        Assert.False(sut.Database.IsOpen);
    }

    [Fact]
    public async Task InactivityTimeout_AlsoRaisesLockedEvent()
    {
        // The UI-navigation contract (Locked fires on auto-lock too, not just explicit Lock()
        // calls) matters most for exactly this path -- there's no UI-initiated call site to
        // otherwise notice the vault just locked itself.
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        int raiseCount = 0;
        sut.Service.Locked += () => raiseCount++;

        sut.Time.Advance(sut.Service.AutoLockTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public async Task NotifyUserActivity_PostponesTheInactivityAutoLock()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        TimeSpan almostTimeout = sut.Service.AutoLockTimeout - TimeSpan.FromSeconds(1);

        sut.Time.Advance(almostTimeout);
        sut.Service.NotifyUserActivity();
        sut.Time.Advance(almostTimeout);

        Assert.True(sut.Service.IsUnlocked);

        sut.Time.Advance(TimeSpan.FromSeconds(2));

        Assert.False(sut.Service.IsUnlocked);
    }

    [Fact]
    public void EncryptWithVaultKey_VaultIsLocked_ThrowsInvalidOperationException()
    {
        Sut sut = CreateSut();

        Assert.Throws<InvalidOperationException>(() => sut.Service.EncryptWithVaultKey([1, 2, 3]));
    }

    [Fact]
    public void DecryptWithVaultKey_VaultIsLocked_ThrowsInvalidOperationException()
    {
        Sut sut = CreateSut();

        Assert.Throws<InvalidOperationException>(() => sut.Service.DecryptWithVaultKey([1, 2, 3]));
    }

    [Fact]
    public async Task EncryptWithVaultKey_ThenDecryptWithVaultKey_RoundTripsThePlaintext()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        byte[] plaintext = "hello vault item"u8.ToArray();

        byte[] ciphertext = sut.Service.EncryptWithVaultKey(plaintext);
        byte[] decrypted = sut.Service.DecryptWithVaultKey(ciphertext);

        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, ciphertext);
    }

    [Fact]
    public async Task DecryptWithVaultKey_AfterLocking_ThrowsInvalidOperationException()
    {
        Sut sut = CreateSut();
        await sut.Service.InitializeVaultAsync(Password("correct horse battery staple"));
        byte[] ciphertext = sut.Service.EncryptWithVaultKey("hello"u8.ToArray());
        sut.Service.Lock();

        Assert.Throws<InvalidOperationException>(() => sut.Service.DecryptWithVaultKey(ciphertext));
    }
}
