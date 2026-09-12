using KeyKeeper.Core.Crypto;

namespace KeyKeeper.Core.Tests.Crypto;

public class SecureMemoryHelperTests
{
    [Fact]
    public void Clear_ByteArray_ZeroesAllBytes()
    {
        byte[] buffer = { 1, 2, 3, 4, 5 };

        SecureMemoryHelper.Clear(buffer);

        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Clear_CharArray_ZeroesAllChars()
    {
        char[] buffer = "secret".ToCharArray();

        SecureMemoryHelper.Clear(buffer);

        Assert.All(buffer, c => Assert.Equal('\0', c));
    }

    [Fact]
    public void Clear_MultipleByteArrays_ZeroesAllOfThem()
    {
        byte[] a = { 1, 2, 3 };
        byte[] b = { 4, 5, 6 };

        SecureMemoryHelper.Clear(a, b);

        Assert.All(a, x => Assert.Equal(0, x));
        Assert.All(b, x => Assert.Equal(0, x));
    }

    [Fact]
    public void Clear_NullOrEmptyBuffers_DoesNotThrow()
    {
        SecureMemoryHelper.Clear((byte[]?)null);
        SecureMemoryHelper.Clear((char[]?)null);
        SecureMemoryHelper.Clear(Array.Empty<byte>());
        SecureMemoryHelper.Clear(Array.Empty<char>());
    }
}
