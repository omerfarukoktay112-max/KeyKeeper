using System.Security.Cryptography;
using KeyKeeper.Core.Crypto;

namespace KeyKeeper.Core.Tests.Crypto;

public class MasterPasswordVerifierTests
{
    private readonly MasterPasswordVerifier _sut = new();
    private readonly Argon2idKeyDerivationService _kdf = new();

    private static byte[] RandomKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Verify_MatchingDerivedKey_ReturnsTrue()
    {
        byte[] derivedKey = RandomKey();
        byte[] verifier = _sut.CreateVerifier(derivedKey);

        Assert.True(_sut.Verify(derivedKey, verifier));
    }

    [Fact]
    public void Verify_NonMatchingDerivedKey_ReturnsFalse()
    {
        byte[] correctKey = RandomKey();
        byte[] wrongKey = RandomKey();
        byte[] verifier = _sut.CreateVerifier(correctKey);

        Assert.False(_sut.Verify(wrongKey, verifier));
    }

    [Fact]
    public void CreateVerifier_SameKey_IsDeterministic()
    {
        byte[] key = RandomKey();

        byte[] verifierA = _sut.CreateVerifier(key);
        byte[] verifierB = _sut.CreateVerifier(key);

        Assert.Equal(verifierA, verifierB);
    }

    [Fact]
    public void CreateVerifier_DifferentKeys_ProduceDifferentVerifiers()
    {
        byte[] verifierA = _sut.CreateVerifier(RandomKey());
        byte[] verifierB = _sut.CreateVerifier(RandomKey());

        Assert.NotEqual(verifierA, verifierB);
    }

    [Fact]
    public void EndToEnd_CorrectMasterPassword_VerifiesSuccessfully()
    {
        char[] masterPassword = "correct horse battery staple".ToCharArray();
        byte[] salt = _kdf.GenerateSalt();

        byte[] registrationKey = _kdf.DeriveKey(masterPassword, salt, TestArgon2Parameters.Fast);
        byte[] storedVerifier = _sut.CreateVerifier(registrationKey);

        byte[] loginKey = _kdf.DeriveKey("correct horse battery staple".ToCharArray(), salt, TestArgon2Parameters.Fast);

        Assert.True(_sut.Verify(loginKey, storedVerifier));
    }

    [Fact]
    public void EndToEnd_WrongMasterPassword_FailsVerification()
    {
        byte[] salt = _kdf.GenerateSalt();

        byte[] registrationKey = _kdf.DeriveKey("correct horse battery staple".ToCharArray(), salt, TestArgon2Parameters.Fast);
        byte[] storedVerifier = _sut.CreateVerifier(registrationKey);

        byte[] loginKey = _kdf.DeriveKey("wrong guess entirely".ToCharArray(), salt, TestArgon2Parameters.Fast);

        Assert.False(_sut.Verify(loginKey, storedVerifier));
    }
}
