namespace KeyKeeper.Core.Backup;

/// <summary>
/// Thrown when a file isn't a structurally valid .vault backup at all — wrong/missing magic
/// header, a truncated length prefix, or header JSON that doesn't parse. Distinct from
/// <see cref="UnsupportedBackupVersionException"/> (a structurally valid file this build simply
/// doesn't support yet) and from a wrong master password (a structurally valid file that fails
/// to decrypt) — see <c>IBackupService.PrepareImportAsync</c>'s doc comment for why all three are
/// reported to the user distinctly.
/// </summary>
public sealed class VaultBackupFormatException : Exception
{
    public VaultBackupFormatException(string message)
        : base(message)
    {
    }

    public VaultBackupFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
