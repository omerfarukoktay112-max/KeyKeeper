using System.Text;
using System.Text.Json;

namespace KeyKeeper.Core.Backup;

/// <summary>
/// The on-disk byte layout of a .vault file: <c>MAGIC (8 bytes) || header length (4-byte little-
/// endian int32) || header JSON (UTF-8, <see cref="VaultBackupHeader"/>) || encrypted payload
/// (the rest of the file)</c>.
///
/// A length-prefixed binary framing was chosen over, say, a text delimiter (a newline after the
/// header) specifically so the header's own JSON content can never be misread as part of the
/// separator, and so the encrypted payload — arbitrary binary bytes, not text — never needs any
/// escaping to sit right after it. <see cref="Magic"/> exists purely so <see cref="Unpack"/> can
/// reject an unrelated file immediately, with a clear message, rather than failing confusingly
/// deep inside JSON parsing or AES-GCM decryption.
/// </summary>
public static class VaultBackupFile
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("KKVLTBKP");

    public static byte[] Pack(VaultBackupHeader header, byte[] encryptedPayload)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(encryptedPayload);

        byte[] headerJson = JsonSerializer.SerializeToUtf8Bytes(header);
        byte[] lengthPrefix = BitConverter.GetBytes(headerJson.Length);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthPrefix);
        }

        var file = new byte[Magic.Length + lengthPrefix.Length + headerJson.Length + encryptedPayload.Length];
        int offset = 0;

        Buffer.BlockCopy(Magic, 0, file, offset, Magic.Length);
        offset += Magic.Length;

        Buffer.BlockCopy(lengthPrefix, 0, file, offset, lengthPrefix.Length);
        offset += lengthPrefix.Length;

        Buffer.BlockCopy(headerJson, 0, file, offset, headerJson.Length);
        offset += headerJson.Length;

        Buffer.BlockCopy(encryptedPayload, 0, file, offset, encryptedPayload.Length);

        return file;
    }

    /// <exception cref="VaultBackupFormatException">
    /// The file is too short, doesn't start with the expected magic bytes, its header length
    /// prefix is out of range, or its header JSON doesn't parse. Never thrown for a wrong
    /// password or an unsupported format version — those are only detectable after this method
    /// succeeds (see <c>IBackupService.PrepareImportAsync</c>).
    /// </exception>
    public static (VaultBackupHeader Header, byte[] EncryptedPayload) Unpack(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        int prefixEnd = Magic.Length + sizeof(int);
        if (fileBytes.Length < prefixEnd)
        {
            throw new VaultBackupFormatException("Dosya çok kısa; geçerli bir KeyKeeper yedek dosyası değil.");
        }

        if (!fileBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new VaultBackupFormatException("Dosya imzası tanınmadı; geçerli bir KeyKeeper yedek dosyası değil.");
        }

        byte[] lengthPrefix = fileBytes[Magic.Length..prefixEnd];
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthPrefix);
        }

        int headerLength = BitConverter.ToInt32(lengthPrefix, 0);
        if (headerLength < 0 || prefixEnd + headerLength > fileBytes.Length)
        {
            throw new VaultBackupFormatException("Dosya kısaltılmış veya bozulmuş görünüyor.");
        }

        byte[] headerJson = fileBytes[prefixEnd..(prefixEnd + headerLength)];
        VaultBackupHeader? header;
        try
        {
            header = JsonSerializer.Deserialize<VaultBackupHeader>(headerJson);
        }
        catch (JsonException ex)
        {
            throw new VaultBackupFormatException("Yedek başlığı okunamadı; dosya bozulmuş olabilir.", ex);
        }

        if (header is null)
        {
            throw new VaultBackupFormatException("Yedek başlığı boş; dosya bozulmuş olabilir.");
        }

        byte[] encryptedPayload = fileBytes[(prefixEnd + headerLength)..];
        if (encryptedPayload.Length == 0)
        {
            throw new VaultBackupFormatException("Yedek dosyasında şifreli veri bulunamadı.");
        }

        return (header, encryptedPayload);
    }
}
