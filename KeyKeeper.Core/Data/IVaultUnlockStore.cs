namespace KeyKeeper.Core.Data;

/// <summary>
/// Stores the data needed to attempt a vault unlock <em>before</em> the SQLCipher-encrypted
/// vault database can be opened: the Argon2id salt/parameters, the master-password verifier,
/// and the persisted lockout counter. Backed by its own small, deliberately unencrypted SQLite
/// file — see <see cref="VaultUnlockStore"/>'s doc comment for why this can't simply be a table
/// inside the encrypted database.
/// </summary>
public interface IVaultUnlockStore
{
    /// <summary>Whether <see cref="WriteMetadata"/> has ever been called (vault set up).</summary>
    bool IsInitialized { get; }

    /// <summary>Returns the stored unlock metadata, or null if the vault hasn't been set up yet.</summary>
    VaultUnlockMetadata? ReadMetadata();

    /// <summary>Writes (creating or overwriting) the unlock metadata.</summary>
    void WriteMetadata(VaultUnlockMetadata metadata);

    /// <summary>Returns the current lockout state, or <see cref="LockoutState.None"/> if never recorded.</summary>
    LockoutState ReadLockoutState();

    /// <summary>Writes (creating or overwriting) the lockout state.</summary>
    void WriteLockoutState(LockoutState state);
}
