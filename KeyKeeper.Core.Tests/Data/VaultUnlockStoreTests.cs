using KeyKeeper.Core.Data;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class VaultUnlockStoreTests
{
    private sealed class TempPathProvider : IVaultStoragePathProvider
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"keykeeper-test-{Guid.NewGuid():N}");

        public string GetDatabaseFilePath() => Path.Combine(Directory, "vault.db3");

        public string GetUnlockMetadataFilePath() => Path.Combine(Directory, "vault-unlock.db3");

        public void Cleanup()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    private static VaultUnlockMetadata SampleMetadata() => new(
        SchemaVersion: 1,
        Salt: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16],
        Argon2MemorySizeKiB: 19_456,
        Argon2Iterations: 2,
        Argon2Parallelism: 1,
        Argon2OutputLengthBytes: 32,
        MasterPasswordVerifier: [9, 9, 9, 9],
        CreatedAtUtc: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void IsInitialized_BeforeAnyWrite_IsFalse()
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);

            Assert.False(sut.IsInitialized);
            Assert.Null(sut.ReadMetadata());
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void WriteMetadata_ThenIsInitialized_IsTrue()
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);

            sut.WriteMetadata(SampleMetadata());

            Assert.True(sut.IsInitialized);
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void WriteMetadata_ThenReadMetadata_RoundTripsAllFields()
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);
            VaultUnlockMetadata expected = SampleMetadata();

            sut.WriteMetadata(expected);
            VaultUnlockMetadata? actual = sut.ReadMetadata();

            Assert.NotNull(actual);
            Assert.Equal(expected.SchemaVersion, actual!.SchemaVersion);
            Assert.Equal(expected.Salt, actual.Salt);
            Assert.Equal(expected.Argon2MemorySizeKiB, actual.Argon2MemorySizeKiB);
            Assert.Equal(expected.Argon2Iterations, actual.Argon2Iterations);
            Assert.Equal(expected.Argon2Parallelism, actual.Argon2Parallelism);
            Assert.Equal(expected.Argon2OutputLengthBytes, actual.Argon2OutputLengthBytes);
            Assert.Equal(expected.MasterPasswordVerifier, actual.MasterPasswordVerifier);
            Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void WriteMetadata_CalledTwice_OverwritesThePreviousValue()
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);
            sut.WriteMetadata(SampleMetadata());

            VaultUnlockMetadata updated = SampleMetadata() with { SchemaVersion = 2, Argon2Iterations = 3 };
            sut.WriteMetadata(updated);

            VaultUnlockMetadata? actual = sut.ReadMetadata();
            Assert.Equal(2, actual!.SchemaVersion);
            Assert.Equal(3, actual.Argon2Iterations);
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void ReadLockoutState_BeforeAnyWrite_ReturnsNone()
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);

            Assert.Equal(LockoutState.None, sut.ReadLockoutState());
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Theory]
    [MemberData(nameof(LockoutStates))]
    public void WriteLockoutState_ThenReadLockoutState_RoundTrips(LockoutState state)
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);

            sut.WriteLockoutState(state);

            Assert.Equal(state, sut.ReadLockoutState());
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    public static TheoryData<LockoutState> LockoutStates() =>
    [
        new LockoutState(3, null),
        new LockoutState(5, new DateTimeOffset(2026, 6, 1, 8, 30, 0, TimeSpan.Zero)),
    ];

    [Fact]
    public void WriteLockoutState_CalledTwice_OverwritesThePreviousValue()
    {
        var pathProvider = new TempPathProvider();
        try
        {
            using var sut = new VaultUnlockStore(pathProvider);
            sut.WriteLockoutState(new LockoutState(3, null));

            sut.WriteLockoutState(LockoutState.None);

            Assert.Equal(LockoutState.None, sut.ReadLockoutState());
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void Metadata_WrittenByOneInstance_IsVisibleToAFreshInstanceOnTheSameFile()
    {
        // Simulates the app relaunching: a new VaultUnlockStore (and underlying connection)
        // must see what a previous instance persisted to the same unlock-metadata file.
        var pathProvider = new TempPathProvider();
        try
        {
            using (var first = new VaultUnlockStore(pathProvider))
            {
                first.WriteMetadata(SampleMetadata());
                first.WriteLockoutState(new LockoutState(4, null));
            }

            using var second = new VaultUnlockStore(pathProvider);

            Assert.True(second.IsInitialized);
            Assert.Equal(SampleMetadata().SchemaVersion, second.ReadMetadata()!.SchemaVersion);
            Assert.Equal(4, second.ReadLockoutState().FailedAttemptCount);
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }
}
