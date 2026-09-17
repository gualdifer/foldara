using Xunit;

namespace Foldara.Storage.Tests;

public sealed class StorageProviderCapabilitiesTests
{
    [Fact]
    public void ConstructorRepresentsProviderBehavior()
    {
        var capabilities = new StorageProviderCapabilities(
            StoragePathCaseSensitivity.Insensitive,
            hasStableItemIdentifiers: true,
            supportsMove: true,
            supportsAtomicMove: true,
            supportsAtomicReplace: true,
            supportsDelete: true,
            supportsTrash: true,
            supportsChangeFeed: true,
            supportsResumableWrites: true,
            providesContentHashes: true,
            supportsVersioning: true);

        Assert.Equal(StoragePathCaseSensitivity.Insensitive, capabilities.PathCaseSensitivity);
        Assert.True(capabilities.HasStableItemIdentifiers);
        Assert.True(capabilities.SupportsMove);
        Assert.True(capabilities.SupportsAtomicMove);
        Assert.True(capabilities.SupportsAtomicReplace);
        Assert.True(capabilities.SupportsDelete);
        Assert.True(capabilities.SupportsTrash);
        Assert.True(capabilities.SupportsChangeFeed);
        Assert.True(capabilities.SupportsResumableWrites);
        Assert.True(capabilities.ProvidesContentHashes);
        Assert.True(capabilities.SupportsVersioning);
    }

    [Fact]
    public void ConstructorUsesConservativeDefaults()
    {
        var capabilities = new StorageProviderCapabilities(StoragePathCaseSensitivity.Sensitive);

        Assert.False(capabilities.HasStableItemIdentifiers);
        Assert.False(capabilities.SupportsMove);
        Assert.False(capabilities.SupportsAtomicMove);
        Assert.False(capabilities.SupportsAtomicReplace);
        Assert.False(capabilities.SupportsDelete);
        Assert.False(capabilities.SupportsTrash);
        Assert.False(capabilities.SupportsChangeFeed);
        Assert.False(capabilities.SupportsResumableWrites);
        Assert.False(capabilities.ProvidesContentHashes);
        Assert.False(capabilities.SupportsVersioning);
    }

    [Fact]
    public void ConstructorRejectsUnknownCaseSensitivity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StorageProviderCapabilities((StoragePathCaseSensitivity)42));
    }

    [Fact]
    public void ConstructorRejectsAtomicMoveWithoutMoveSupport()
    {
        Assert.Throws<ArgumentException>(() =>
            new StorageProviderCapabilities(
                StoragePathCaseSensitivity.Sensitive,
                supportsAtomicMove: true));
    }

    [Fact]
    public void ConstructorRejectsTrashWithoutDeleteSupport()
    {
        Assert.Throws<ArgumentException>(() =>
            new StorageProviderCapabilities(
                StoragePathCaseSensitivity.Sensitive,
                supportsTrash: true));
    }
}
