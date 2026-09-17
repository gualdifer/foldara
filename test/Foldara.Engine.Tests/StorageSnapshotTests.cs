using Foldara.Core;
using Foldara.Storage;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class StorageSnapshotTests
{
    [Fact]
    public void ConstructorOrdersEntriesByNormalizedPath()
    {
        var snapshot = new StorageSnapshot(
            StoragePathCaseSensitivity.Sensitive,
        [
            File("z.txt"),
            File("a.txt"),
            File("folder/file.txt"),
            Directory("folder"),
        ]);

        Assert.Equal(
            ["a.txt", "folder", "folder/file.txt", "z.txt"],
            snapshot.Entries.Select(entry => entry.Path.Value));
    }

    [Fact]
    public void CaseInsensitiveSnapshotRejectsPathsThatDifferOnlyByCase()
    {
        Assert.Throws<ArgumentException>(() => new StorageSnapshot(
            StoragePathCaseSensitivity.Insensitive,
            [File("File.txt"), File("file.txt")]));
    }

    [Fact]
    public void LookupUsesProviderCaseSensitivity()
    {
        var entry = File("File.txt");
        var snapshot = new StorageSnapshot(
            StoragePathCaseSensitivity.Insensitive,
            [entry]);

        var found = snapshot.TryGetEntry(StoragePath.Parse("file.txt"), out var result);

        Assert.True(found);
        Assert.Same(entry, result);
    }

    [Fact]
    public void ConstructorDefensivelyCopiesEntries()
    {
        var entries = new List<StorageEntry> { File("file.txt") };
        var snapshot = new StorageSnapshot(StoragePathCaseSensitivity.Sensitive, entries);

        entries.Clear();

        Assert.Single(snapshot.Entries);
    }

    [Fact]
    public void ConstructorRejectsRootAndNullEntries()
    {
        Assert.Throws<ArgumentException>(() => new StorageSnapshot(
            StoragePathCaseSensitivity.Sensitive,
            [new StorageEntry(StoragePath.Root, StorageEntryKind.Directory)]));
        Assert.Throws<ArgumentException>(() => new StorageSnapshot(
            StoragePathCaseSensitivity.Sensitive,
            new StorageEntry[] { null! }));
    }

    private static StorageEntry File(string path) =>
        new(StoragePath.Parse(path), StorageEntryKind.File, new StorageEntryMetadata(size: 0));

    private static StorageEntry Directory(string path) =>
        new(StoragePath.Parse(path), StorageEntryKind.Directory);
}
