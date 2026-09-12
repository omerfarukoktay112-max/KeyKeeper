namespace KeyKeeper.Core.Auth;

/// <summary>
/// Orchestrates unlocking/locking the vault: first-run setup, master-password login,
/// biometric login, persisted lockout, and the inactivity auto-lock timer. Ties together
/// <c>KeyKeeper.Core.Crypto</c> (Phase 1) and <c>KeyKeeper.Core.Data</c>/<c>Storage</c>
/// (Phase 2) into one place so ViewModels don't have to.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Whether a master password has ever been set up (<see cref="InitializeVaultAsync"/> called).</summary>
    bool IsVaultInitialized { get; }

    /// <summary>Whether the vault database is currently open.</summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Raised whenever the vault transitions from unlocked to locked — whether via an explicit
    /// <see cref="Lock"/> call or the inactivity auto-lock timer firing. Not raised by calling
    /// <see cref="Lock"/> while already locked. UI code (a Shell, say) is the intended
    /// subscriber, to navigate back to a lock screen the moment this fires — including when
    /// nothing in the UI itself initiated the lock. Raised on whatever thread caused the
    /// transition, which for the inactivity timer is <em>not</em> the UI thread; subscribers
    /// that touch UI must marshal accordingly.
    /// </summary>
    event Action? Locked;

    /// <summary>Inactivity auto-lock duration. Intended range: 1-5 minutes.</summary>
    TimeSpan AutoLockTimeout { get; set; }

    /// <summary>
    /// First-run setup: derives a key from <paramref name="masterPassword"/> with a fresh salt,
    /// stores the salt/parameters/verifier, and creates+opens the vault database with that key.
    /// The vault is unlocked immediately afterward (Success) — the user doesn't have to log in
    /// again right after setting up.
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault has already been initialized.</exception>
    Task<AuthenticationResult> InitializeVaultAsync(char[] masterPassword);

    /// <summary>
    /// Re-derives a key from <paramref name="masterPassword"/> and the stored salt, compares it
    /// against the stored verifier, and if it matches, opens the vault database with it.
    /// Subject to lockout (see <see cref="LockoutPolicy"/>) — a locked-out call returns
    /// <see cref="AuthenticationResultKind.LockedOut"/> without deriving anything.
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault hasn't been initialized yet.</exception>
    Task<AuthenticationResult> UnlockWithMasterPasswordAsync(char[] masterPassword);

    /// <summary>
    /// Prompts for biometric authentication and, on success, unlocks the vault using the key
    /// stored via <c>KeyKeeper.Core.Storage.IWrappedKeyStorage</c>. Does not consume a failed
    /// master-password attempt or affect the lockout counter — a cancelled/failed biometric
    /// prompt is not a wrong-password guess.
    /// </summary>
    Task<AuthenticationResult> TryUnlockWithBiometricsAsync();

    /// <summary>
    /// Stores the currently-held vault key via <c>IWrappedKeyStorage</c>, enabling
    /// <see cref="TryUnlockWithBiometricsAsync"/> for future unlocks.
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault isn't currently unlocked.</exception>
    Task EnableBiometricUnlockAsync();

    /// <summary>
    /// Locks the vault: stops the inactivity timer, zeroes the in-memory derived key via
    /// <c>SecureMemoryHelper</c>, and closes the open database connection. Safe to call when
    /// already locked.
    /// </summary>
    void Lock();

    /// <summary>Call on any user interaction while unlocked, to postpone the inactivity auto-lock.</summary>
    void NotifyUserActivity();

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under the currently-held vault key (Phase 4:
    /// <c>KeyKeeper.Core.Data.IVaultItemRepository</c> uses this for item payloads). The raw key
    /// never leaves <see cref="AuthenticationService"/> through this call — encryption happens
    /// here, not by handing the key out to a caller — deliberately, so custody of the key stays
    /// exactly as narrow as it already was for Phases 1-3.
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault is locked.</exception>
    byte[] EncryptWithVaultKey(byte[] plaintext);

    /// <summary>The decrypting counterpart to <see cref="EncryptWithVaultKey"/>.</summary>
    /// <exception cref="InvalidOperationException">The vault is locked.</exception>
    /// <exception cref="Crypto.VaultCryptographyException">
    /// The vault key doesn't match what <paramref name="ciphertext"/> was encrypted under, or
    /// <paramref name="ciphertext"/> has been corrupted/tampered with.
    /// </exception>
    byte[] DecryptWithVaultKey(byte[] ciphertext);
}
