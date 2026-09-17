using Xunit;

namespace Foldara.Core.Tests;

public sealed class StorageEntryTests
{
    [Fact]
    public void ConstructorRepresentsAFile()
    {
        var path = StoragePath.Parse("Documents/report.pdf");
        var metadata = new StorageEntryMetadata(size: 123);

        var entry = new StorageEntry(path, StorageEntryKind.File, metadata);

        Assert.Same(path, entry.Path);
        Assert.Equal(StorageEntryKind.File, entry.Kind);
        Assert.Same(metadata, entry.Metadata);
    }

    [Fact]
    public void ConstructorRepresentsADirectoryWithEmptyMetadata()
    {
        var entry = new StorageEntry(
            StoragePath.Parse("Documents"),
            StorageEntryKind.Directory);

        Assert.Equal(StorageEntryKind.Directory, entry.Kind);
        Assert.Null(entry.Metadata.Size);
        Assert.Null(entry.Metadata.ContentHash);
        Assert.Empty(entry.Metadata.ProviderProperties);
    }

    [Fact]
    public void ConstructorAllowsTheStorageRoot()
    {
        var entry = new StorageEntry(StoragePath.Root, StorageEntryKind.Directory);

        Assert.Same(StoragePath.Root, entry.Path);
    }

    [Fact]
    public void ConstructorRejectsNullPath()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StorageEntry(null!, StorageEntryKind.File));
    }

    [Fact]
    public void ConstructorRejectsUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StorageEntry(StoragePath.Root, (StorageEntryKind)42));
    }

    [Fact]
    public void ConstructorRejectsDirectorySize()
    {
        var metadata = new StorageEntryMetadata(size: 0);

        Assert.Throws<ArgumentException>(() =>
            new StorageEntry(StoragePath.Root, StorageEntryKind.Directory, metadata));
    }

    [Fact]
    public void ConstructorRejectsDirectoryContentHash()
    {
        var metadata = new StorageEntryMetadata(
            contentHash: new StorageContentHash("SHA-256", "abc123"));

        Assert.Throws<ArgumentException>(() =>
            new StorageEntry(StoragePath.Root, StorageEntryKind.Directory, metadata));
    }
}
