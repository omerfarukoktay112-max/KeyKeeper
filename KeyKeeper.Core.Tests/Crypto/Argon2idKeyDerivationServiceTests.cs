using KeyKeeper.Core.Crypto;

namespace KeyKeeper.Core.Tests.Crypto;

public class Argon2idKeyDerivationServiceTests
{
    private readonly Argon2idKeyDerivationService _sut = new();

    [Fact]
    public void DeriveKey_SamePasswordAndSalt_IsDeterministic()
    {
        char[] password = "correct horse battery staple".ToCharArray();
        byte[] salt = _sut.GenerateSalt();

        byte[] key1 = _sut.DeriveKey(password, salt, TestArgon2Parameters.Fast);
        byte[] key2 = _sut.DeriveKey(password, salt, TestArgon2Parameters.Fast);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void DeriveKey_OutputLength_MatchesRequestedParameter()
    {
        char[] password = "some password".ToCharArray();
        byte[] salt = _sut.GenerateSalt();
        var parameters = new Argon2Parameters(memorySizeKiB: 8 * 1024, iterations: 1, parallelism: 1, outputLengthBytes: 48);

        byte[] key = _sut.DeriveKey(password, salt, parameters);

        Assert.Equal(48, key.Length);
    }

    [Fact]
    public void DeriveKey_DifferentSalts_ProduceDifferentKeys()
    {
        char[] password = "same password".ToCharArray();
        byte[] saltA = _sut.GenerateSalt();
        byte[] saltB = _sut.GenerateSalt();

        byte[] keyA = _sut.DeriveKey(password, saltA, TestArgon2Parameters.Fast);
        byte[] keyB = _sut.DeriveKey(password, saltB, TestArgon2Parameters.Fast);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void DeriveKey_DifferentPasswords_ProduceDifferentKeys()
    {
        byte[] salt = _sut.GenerateSalt();

        byte[] keyA = _sut.DeriveKey("password one".ToCharArray(), salt, TestArgon2Parameters.Fast);
        byte[] keyB = _sut.DeriveKey("password two".ToCharArray(), salt, TestArgon2Parameters.Fast);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void GenerateSalt_TwoCalls_ProduceDifferentSalts()
    {
        byte[] saltA = _sut.GenerateSalt();
        byte[] saltB = _sut.GenerateSalt();

        Assert.NotEqual(saltA, saltB);
    }

    [Fact]
    public void GenerateSalt_DefaultSize_IsAtLeastMinimum()
    {
        byte[] salt = _sut.GenerateSalt();

        Assert.Equal(Argon2Constants.MinSaltSizeBytes, salt.Length);
    }

    [Fact]
    public void GenerateSalt_BelowMinimumSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GenerateSalt(8));
    }

    [Fact]
    public void DeriveKey_SaltShorterThanMinimum_Throws()
    {
        char[] password = "password".ToCharArray();
        byte[] tooShortSalt = new byte[8];

        Assert.Throws<ArgumentException>(() => _sut.DeriveKey(password, tooShortSalt, TestArgon2Parameters.Fast));
    }

    [Fact]
    public void DeriveKey_EmptyPassword_Throws()
    {
        byte[] salt = _sut.GenerateSalt();

        Assert.Throws<ArgumentException>(() => _sut.DeriveKey(Array.Empty<char>(), salt, TestArgon2Parameters.Fast));
    }
}
