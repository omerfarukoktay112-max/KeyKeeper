using System.Globalization;
using System.Resources;

namespace KeyKeeper.Core;

/// <summary>User-facing strings produced inside <c>KeyKeeper.Core</c> (backup/import errors, biometric prompt reason).
/// English is neutral, Turkish is <c>CoreStrings.tr.resx</c>; the language follows the current UI culture.</summary>
internal static class CoreStrings
{
    private static readonly ResourceManager Resources = new("KeyKeeper.Core.CoreStrings", typeof(CoreStrings).Assembly);

    private static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string BiometricReason => Get(nameof(BiometricReason));
    public static string ExportWrongPassword => Get(nameof(ExportWrongPassword));
    public static string ImportWrongPassword => Get(nameof(ImportWrongPassword));
    public static string CorruptEncryptedBlock => Get(nameof(CorruptEncryptedBlock));
    public static string EmptyBackupContent => Get(nameof(EmptyBackupContent));
    public static string BackupUnreadable(object? arg) => string.Format(CultureInfo.CurrentCulture, Get(nameof(BackupUnreadable)), arg);
    public static string FileTooShort => Get(nameof(FileTooShort));
    public static string FileSignatureUnknown => Get(nameof(FileSignatureUnknown));
    public static string FileTruncated => Get(nameof(FileTruncated));
    public static string HeaderUnreadable => Get(nameof(HeaderUnreadable));
    public static string HeaderEmpty => Get(nameof(HeaderEmpty));
    public static string NoEncryptedData => Get(nameof(NoEncryptedData));
}
