namespace KeyKeeper.Core.Data;

/// <summary>
/// Thrown when the vault database can't be opened or prepared — the key is wrong, the file
/// is corrupted/not a database, or its schema version is newer than this app supports.
/// Wraps the underlying <c>SQLite.SQLiteException</c> (if any) so callers get one clear,
/// documented failure type instead of having to know sqlite-net-pcl's exception shape.
/// </summary>
public sealed class VaultDatabaseException : Exception
{
    public VaultDatabaseException(string message)
        : base(message)
    {
    }

    public VaultDatabaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
