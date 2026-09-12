using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// SQLCipher-backed <see cref="IVaultDatabaseService"/>.
///
/// How the key reaches SQLCipher: sqlite-net-pcl's <see cref="SQLiteConnectionString"/> takes
/// a <c>key</c> parameter typed <c>object</c>. Passed a <c>byte[]</c> (as here), it is handed
/// to the native provider's raw <c>sqlite3_key()</c> call with the bytes as-is — SQLCipher's
/// direct binary-key API — rather than being formatted into a <c>PRAGMA key = "x'...'"</c>
/// hex-string statement (what you'd get by passing a <c>string</c> key instead). Raw bytes are
/// simpler and avoid any hex/quoting formatting mistakes, and match the 256-bit key already
/// produced by <c>KeyKeeper.Core.Crypto.IKeyDerivationService</c> exactly.
///
/// Which native engine actually backs this at runtime (plain SQLite vs. SQLCipher) is a
/// composition-root decision, not this class's: the app registers
/// <c>SQLitePCLRaw.bundle_e_sqlcipher</c> and calls
/// <c>SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher())</c> once at
/// startup (see <c>MauiProgram.cs</c>); tests do the same with the plain
/// <c>SQLite3Provider_e_sqlite3</c> provider (see <c>KeyKeeper.Core.Tests</c>), since the
/// SQLCipher native binary isn't available for desktop RIDs (only Android/iOS).
/// </summary>
public sealed class VaultDatabaseService : IVaultDatabaseService
{
    public const int RequiredKeyLengthBytes = 32;

    private readonly IVaultStoragePathProvider _pathProvider;
    private readonly IVaultMigrationService _migrationService;
    private SQLiteConnection? _connection;

    public VaultDatabaseService(IVaultStoragePathProvider pathProvider, IVaultMigrationService migrationService)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
    }

    public bool IsOpen => _connection is not null;

    public SQLiteConnection Open(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != RequiredKeyLengthBytes)
        {
            throw new ArgumentException(
                $"Vault database key must be exactly {RequiredKeyLengthBytes} bytes (256-bit), was {key.Length}.",
                nameof(key));
        }

        if (_connection is not null)
        {
            return _connection;
        }

        string path = _pathProvider.GetDatabaseFilePath();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SQLiteConnectionString(
            path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create,
            storeDateTimeAsTicks: true,
            key: key);

        var connection = new SQLiteConnection(connectionString);
        bool opened = false;
        try
        {
            connection.Execute("PRAGMA foreign_keys = ON;");

            // sqlite3_open/sqlite3_key don't themselves touch the file -- SQLite(Cipher) opens
            // lazily, and the key is only actually exercised on the first real page read. Force
            // that read now so a wrong key fails immediately and loudly here, with the same
            // SQLITE_NOTADB / "file is not a database" signature SQLCipher always produces for
            // a bad key, instead of failing confusingly on some unrelated later query.
            connection.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master;");

            VaultSchema.EnsureCreated(connection);
            _migrationService.EnsureUpToDate(connection);

            opened = true;
        }
        catch (SQLiteException ex)
        {
            throw new VaultDatabaseException(
                "Failed to open the vault database. The key may be incorrect, or the file may be corrupted.",
                ex);
        }
        finally
        {
            if (!opened)
            {
                connection.Close();
            }
        }

        _connection = connection;
        return connection;
    }

    public void Close()
    {
        _connection?.Close();
        _connection = null;
    }

    public SQLiteConnection GetOpenConnectionOrThrow() =>
        _connection ?? throw new InvalidOperationException("The vault database is not open.");

    public void Dispose() => Close();
}
