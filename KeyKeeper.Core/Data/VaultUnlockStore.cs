using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// <see cref="IVaultUnlockStore"/> backed by its own, separate, deliberately <em>unencrypted</em>
/// SQLite file (see <see cref="IVaultStoragePathProvider.GetUnlockMetadataFilePath"/>).
///
/// Why a separate unencrypted file instead of a table in the encrypted vault database: SQLCipher
/// requires the correct key before it will read anything at all from an encrypted database --
/// including a hypothetical "VaultMetadata" row holding the very salt needed to *derive* that
/// key from the master password. That's an unresolvable chicken-and-egg problem, not a detail
/// to work around locally, so the salt/KDF parameters/verifier live here instead. This is
/// standard practice for password-based envelope encryption generally: a KDF salt and a
/// verification value are not secrets in the way the derived key or the password are -- see
/// <c>KeyKeeper.Core.Crypto.MasterPasswordVerifier</c>'s doc comment for why the verifier
/// specifically doesn't leak the derived key.
///
/// Uses the same globally-selected SQLitePCLRaw provider as <see cref="VaultDatabaseService"/>
/// (see its doc comment) -- but never calls into SQLCipher's keying API, so the connection
/// behaves as ordinary, unencrypted SQLite. This file still lives in private, non-backed-up
/// app storage and is still worth keeping out of backups (see <c>MauiVaultStoragePathProvider</c>):
/// an attacker who obtains it alongside the encrypted vault file gets a faster password-guessing
/// oracle than brute-forcing SQLCipher directly, even though both are ultimately bottlenecked by
/// the same slow Argon2id step.
/// </summary>
public sealed class VaultUnlockStore : IVaultUnlockStore, IDisposable
{
    private const string CreateUnlockTableSql =
        """
        CREATE TABLE IF NOT EXISTS VaultUnlock (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            SchemaVersion INTEGER NOT NULL,
            Salt BLOB NOT NULL,
            Argon2MemorySizeKiB INTEGER NOT NULL,
            Argon2Iterations INTEGER NOT NULL,
            Argon2Parallelism INTEGER NOT NULL,
            Argon2OutputLengthBytes INTEGER NOT NULL,
            MasterPasswordVerifier BLOB NOT NULL,
            CreatedAtUtc INTEGER NOT NULL
        );
        """;

    private const string CreateLockoutTableSql =
        """
        CREATE TABLE IF NOT EXISTS LockoutState (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            FailedAttemptCount INTEGER NOT NULL DEFAULT 0,
            LockedUntilUtc INTEGER NULL
        );
        """;

    private readonly IVaultStoragePathProvider _pathProvider;
    private readonly object _gate = new();
    private SQLiteConnection? _connection;

    public VaultUnlockStore(IVaultStoragePathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
    }

    private SQLiteConnection Connection
    {
        get
        {
            lock (_gate)
            {
                if (_connection is null)
                {
                    string path = _pathProvider.GetUnlockMetadataFilePath();
                    string? directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var connection = new SQLiteConnection(path);
                    connection.Execute(CreateUnlockTableSql);
                    connection.Execute(CreateLockoutTableSql);
                    _connection = connection;
                }

                return _connection;
            }
        }
    }

    public bool IsInitialized =>
        Connection.ExecuteScalar<int>("SELECT count(*) FROM VaultUnlock WHERE Id = 1;") > 0;

    public VaultUnlockMetadata? ReadMetadata()
    {
        UnlockRow? row = Connection.Query<UnlockRow>("SELECT * FROM VaultUnlock WHERE Id = 1;").FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new VaultUnlockMetadata(
            row.SchemaVersion,
            row.Salt,
            row.Argon2MemorySizeKiB,
            row.Argon2Iterations,
            row.Argon2Parallelism,
            row.Argon2OutputLengthBytes,
            row.MasterPasswordVerifier,
            new DateTimeOffset(row.CreatedAtUtc, TimeSpan.Zero));
    }

    public void WriteMetadata(VaultUnlockMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        Connection.Execute(
            """
            INSERT INTO VaultUnlock
                (Id, SchemaVersion, Salt, Argon2MemorySizeKiB, Argon2Iterations, Argon2Parallelism, Argon2OutputLengthBytes, MasterPasswordVerifier, CreatedAtUtc)
            VALUES
                (1, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT (Id) DO UPDATE SET
                SchemaVersion = excluded.SchemaVersion,
                Salt = excluded.Salt,
                Argon2MemorySizeKiB = excluded.Argon2MemorySizeKiB,
                Argon2Iterations = excluded.Argon2Iterations,
                Argon2Parallelism = excluded.Argon2Parallelism,
                Argon2OutputLengthBytes = excluded.Argon2OutputLengthBytes,
                MasterPasswordVerifier = excluded.MasterPasswordVerifier,
                CreatedAtUtc = excluded.CreatedAtUtc;
            """,
            metadata.SchemaVersion,
            metadata.Salt,
            metadata.Argon2MemorySizeKiB,
            metadata.Argon2Iterations,
            metadata.Argon2Parallelism,
            metadata.Argon2OutputLengthBytes,
            metadata.MasterPasswordVerifier,
            metadata.CreatedAtUtc.UtcTicks);
    }

    public LockoutState ReadLockoutState()
    {
        LockoutRow? row = Connection.Query<LockoutRow>("SELECT * FROM LockoutState WHERE Id = 1;").FirstOrDefault();
        if (row is null)
        {
            return LockoutState.None;
        }

        DateTimeOffset? lockedUntil = row.LockedUntilUtc is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
        return new LockoutState(row.FailedAttemptCount, lockedUntil);
    }

    public void WriteLockoutState(LockoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Connection.Execute(
            """
            INSERT INTO LockoutState (Id, FailedAttemptCount, LockedUntilUtc)
            VALUES (1, ?, ?)
            ON CONFLICT (Id) DO UPDATE SET
                FailedAttemptCount = excluded.FailedAttemptCount,
                LockedUntilUtc = excluded.LockedUntilUtc;
            """,
            state.FailedAttemptCount,
            state.LockedUntilUtc?.UtcTicks);
    }

    /// <summary>Closes the underlying connection, if open. Safe to call more than once.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Close();
            _connection = null;
        }
    }

    private sealed class UnlockRow
    {
        public int SchemaVersion { get; set; }
        public byte[] Salt { get; set; } = [];
        public int Argon2MemorySizeKiB { get; set; }
        public int Argon2Iterations { get; set; }
        public int Argon2Parallelism { get; set; }
        public int Argon2OutputLengthBytes { get; set; }
        public byte[] MasterPasswordVerifier { get; set; } = [];
        public long CreatedAtUtc { get; set; }
    }

    private sealed class LockoutRow
    {
        public int FailedAttemptCount { get; set; }
        public long? LockedUntilUtc { get; set; }
    }
}
