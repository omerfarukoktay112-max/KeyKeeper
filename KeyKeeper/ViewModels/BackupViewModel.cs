using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Backup;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace KeyKeeper.ViewModels;

/// <summary>
/// Drives "Yedekleme" (Phase 8): whole-vault .vault export/import via <see cref="IBackupService"/>
/// and the warned-about plaintext CSV export via <see cref="ICsvExportService"/>. One shared,
/// masked <see cref="MasterPassword"/> field feeds all three actions below — re-typed and
/// re-verified for each one (see <see cref="IBackupService.VerifyMasterPasswordAsync"/>'s doc
/// comment), never reused from whatever unlocked the vault originally, so a hijacked UI session
/// still can't export/restore/CSV-dump without the real master password.
///
/// No <c>MAUI FileSaver</c> package is used for "save the exported file wherever the user picks"
/// — this app already keeps its dependency footprint deliberately small (see
/// <c>ARCHITECTURE.md</c>'s Packages table), and the built-in
/// <see cref="Microsoft.Maui.ApplicationModel.DataTransfer.Share"/> API already covers it: both
/// Android's and iOS's native share sheets include a "Save to Files" destination alongside
/// whatever apps can receive the file, so sharing a temp file accomplishes the same "user picks
/// where it ends up" outcome without a new dependency. The exported file is first written to
/// <see cref="FileSystem.CacheDirectory"/> (app-private and, on both platforms, excluded from
/// backups by OS convention — see <c>KeyKeeper.Services.MauiVaultStoragePathProvider</c>'s doc
/// comment for the equivalent reasoning applied to the vault database itself) purely so
/// <see cref="Share"/> has a real file path to hand off; nothing about that temp copy is itself
/// the persisted backup.
/// </summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private readonly IBackupService _backup;
    private readonly ICsvExportService _csvExport;
    private readonly IVaultItemRepository _items;

    public BackupViewModel(IBackupService backup, ICsvExportService csvExport, IVaultItemRepository items)
    {
        _backup = backup;
        _csvExport = csvExport;
        _items = items;
    }

    [ObservableProperty]
    private string masterPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreActionsEnabled))]
    private bool isBusy;

    public bool AreActionsEnabled => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? statusMessage;

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task ExportVaultAsync()
    {
        if (!TryTakePassword(out char[] passwordChars))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Ana parola doğrulanıyor ve yedek şifreleniyor... (bu birkaç saniye sürebilir)";
        try
        {
            BackupExportResult result = await _backup.ExportAsync(passwordChars);
            if (result.Kind == BackupExportResultKind.WrongPassword)
            {
                StatusMessage = null;
                ErrorMessage = result.ErrorMessage;
                return;
            }

            string path = Path.Combine(FileSystem.CacheDirectory, $"keykeeper-yedek-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.vault");
            await File.WriteAllBytesAsync(path, result.FileBytes!);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "KeyKeeper Yedeğini Kaydet",
                File = new ShareFile(path),
            });

            StatusMessage = "Yedek oluşturuldu.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            StatusMessage = null;
            ErrorMessage = "Kasa kilitlendi. Lütfen tekrar açıp deneyin.";
        }
        finally
        {
            SecureMemoryHelper.Clear(passwordChars);
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportVaultAsync()
    {
        if (string.IsNullOrEmpty(MasterPassword))
        {
            ErrorMessage = "Lütfen ana parolanızı girin.";
            return;
        }

        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Yedek dosyasını seçin (.vault)" });
        }
        catch (Exception)
        {
            ErrorMessage = "Dosya seçilemedi.";
            return;
        }

        if (picked is null)
        {
            return;
        }

        if (!TryTakePassword(out char[] passwordChars))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Yedek dosyası okunuyor ve doğrulanıyor...";
        try
        {
            BackupImportResult result = await _backup.PrepareImportAsync(picked.FullPath, passwordChars);
            if (result.Kind != BackupImportResultKind.Success)
            {
                StatusMessage = null;
                ErrorMessage = result.ErrorMessage;
                return;
            }

            BackupImportPreview preview = result.Preview!;
            bool confirmed = await Shell.Current.DisplayAlert(
                "Kasa Değiştirilecek",
                $"{preview.ItemCount} öğe, {preview.FolderCount} klasör, {preview.TagCount} etiket içe aktarılacak. " +
                "Mevcut kasanızın TÜM içeriği silinecek ve bunların yerine geçecek. Bu işlem GERİ ALINAMAZ.",
                "İçe Aktar", "Vazgeç");
            if (!confirmed)
            {
                StatusMessage = "İçe aktarma iptal edildi.";
                return;
            }

            StatusMessage = "İçe aktarılıyor...";
            await _backup.CommitImportAsync(preview);
            StatusMessage = "Yedek başarıyla içe aktarıldı.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            StatusMessage = null;
            ErrorMessage = "Kasa kilitlendi. Lütfen tekrar açıp deneyin.";
        }
        catch (IOException)
        {
            StatusMessage = null;
            ErrorMessage = "Dosya okunamadı.";
        }
        finally
        {
            SecureMemoryHelper.Clear(passwordChars);
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        bool acknowledged = await Shell.Current.DisplayAlert(
            "Şifresiz Dışa Aktarım",
            "Bu CSV dosyası tüm parolalarınızı ve diğer gizli bilgilerinizi ŞİFRESİZ, düz metin " +
            "olarak içerecek. Dosya, kaydettiğiniz veya paylaştığınız her yerde güvensiz biçimde " +
            "durabilir. Yine de devam etmek istiyor musunuz?",
            "Devam Et", "Vazgeç");
        if (!acknowledged)
        {
            return;
        }

        if (!TryTakePassword(out char[] passwordChars))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Ana parola doğrulanıyor...";
        try
        {
            bool verified = await _backup.VerifyMasterPasswordAsync(passwordChars);
            if (!verified)
            {
                StatusMessage = null;
                ErrorMessage = "Ana parola yanlış.";
                return;
            }

            IReadOnlyList<VaultItemRecord> items = _items.List();
            string csv = _csvExport.Export(items);

            string path = Path.Combine(FileSystem.CacheDirectory, $"keykeeper-export-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv");
            await File.WriteAllTextAsync(path, csv);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "KeyKeeper CSV Dışa Aktarımını Kaydet",
                File = new ShareFile(path),
            });

            StatusMessage = "CSV dışa aktarıldı.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            StatusMessage = null;
            ErrorMessage = "Kasa kilitlendi. Lütfen tekrar açıp deneyin.";
        }
        finally
        {
            SecureMemoryHelper.Clear(passwordChars);
            IsBusy = false;
        }
    }

    /// <summary>Validates <see cref="MasterPassword"/> is non-empty, hands the caller an owned
    /// <c>char[]</c> copy to clear themselves once done, and clears the bound (string) property
    /// immediately -- the same acknowledged string-boundary tradeoff as every other master-password
    /// entry point in this app (see <c>SetupViewModel.SubmitAsync</c>).</summary>
    private bool TryTakePassword(out char[] passwordChars)
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrEmpty(MasterPassword))
        {
            ErrorMessage = "Lütfen ana parolanızı girin.";
            passwordChars = [];
            return false;
        }

        passwordChars = MasterPassword.ToCharArray();
        MasterPassword = string.Empty;
        return true;
    }
}
