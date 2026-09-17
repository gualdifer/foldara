using System.Runtime.CompilerServices;
using Foldara.Core;
using Foldara.Storage;
using Foldara.Storage.Local;
using Foldara.Testing;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class StorageScannerTests
{
    [Fact]
    public async Task ScanRecursesAndProducesDeterministicOrder()
    {
        var provider = new StubStorageProvider()
            .AddChildren(StoragePath.Root, File("z.txt"), Directory("folder"), File("a.txt"))
            .AddChildren(
                StoragePath.Parse("folder"),
                File("folder/z.txt"),
                File("folder/a.txt"));
        var scanner = new StorageScanner();

        var snapshot = await scanner.ScanAsync(
            provider,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["a.txt", "folder", "folder/a.txt", "folder/z.txt", "z.txt"],
            snapshot.Entries.Select(entry => entry.Path.Value));
    }

    [Fact]
    public async Task ScanReturnsEmptySnapshotForEmptyRoot()
    {
        var provider = new StubStorageProvider();
        var scanner = new StorageScanner();

        var snapshot = await scanner.ScanAsync(
            provider,
            TestContext.Current.CancellationToken);

        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public async Task ScanIntegratesWithLocalProvider()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("z.txt"), "z")
            .CreateDirectory(StoragePath.Parse("empty"))
            .WriteTextFile(StoragePath.Parse("nested/file.txt"), "contents");
        var provider = new LocalStorageProvider(directory.RootPath);
        var scanner = new StorageScanner();

        var snapshot = await scanner.ScanAsync(
            provider,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["empty", "nested", "nested/file.txt", "z.txt"],
            snapshot.Entries.Select(entry => entry.Path.Value));
    }

    [Fact]
    public async Task ScanSkipsDirectoryThatDisappearsBeforeEnumeration()
    {
        var volatilePath = StoragePath.Parse("volatile");
        var provider = new StubStorageProvider()
            .AddChildren(StoragePath.Root, Directory("volatile"), File("stable.txt"))
            .FailEnumeration(volatilePath, StorageProviderError.NotFound);
        var scanner = new StorageScanner();

        var snapshot = await scanner.ScanAsync(
            provider,
            TestContext.Current.CancellationToken);

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal("stable.txt", entry.Path.Value);
    }

    [Fact]
    public async Task ScanPropagatesAccessFailureWithoutReturningPartialSnapshot()
    {
        var provider = new StubStorageProvider()
            .FailEnumeration(StoragePath.Root, StorageProviderError.AccessDenied);
        var scanner = new StorageScanner();

        var exception = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await scanner.ScanAsync(provider, TestContext.Current.CancellationToken));

        Assert.Equal(StorageProviderError.AccessDenied, exception.Error);
    }

    [Fact]
    public async Task ScanRejectsEntryThatIsNotADirectChild()
    {
        var provider = new StubStorageProvider()
            .AddChildren(StoragePath.Root, File("nested/file.txt"));
        var scanner = new StorageScanner();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await scanner.ScanAsync(provider, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScanRejectsDuplicatePathsUsingProviderSemantics()
    {
        var provider = new StubStorageProvider(StoragePathCaseSensitivity.Insensitive)
            .AddChildren(StoragePath.Root, File("File.txt"), File("file.txt"));
        var scanner = new StorageScanner();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await scanner.ScanAsync(provider, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScanHonorsPreCancelledToken()
    {
        var provider = new StubStorageProvider();
        var scanner = new StorageScanner();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scanner.ScanAsync(provider, cancellation.Token));
    }

    private static StorageEntry File(string path) =>
        new(StoragePath.Parse(path), StorageEntryKind.File, new StorageEntryMetadata(size: 0));

    private static StorageEntry Directory(string path) =>
        new(StoragePath.Parse(path), StorageEntryKind.Directory);

    private sealed class StubStorageProvider : IStorageProvider
    {
        private readonly Dictionary<StoragePath, IReadOnlyList<StorageEntry>> _children = [];
        private readonly Dictionary<StoragePath, StorageProviderError> _enumerationFailures = [];

        public StubStorageProvider(
            StoragePathCaseSensitivity pathCaseSensitivity = StoragePathCaseSensitivity.Sensitive)
        {
            Capabilities = new StorageProviderCapabilities(pathCaseSensitivity);
        }

        public StorageProviderCapabilities Capabilities { get; }

        public StubStorageProvider AddChildren(
            StoragePath directory,
            params StorageEntry[] entries)
        {
            _children.Add(directory, entries);
            return this;
        }

        public StubStorageProvider FailEnumeration(
            StoragePath directory,
            StorageProviderError error)
        {
            _enumerationFailures.Add(directory, error);
            return this;
        }

        public ValueTask<StorageEntry?> GetEntryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<StorageEntry> EnumerateChildrenAsync(
            StoragePath directory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            if (_enumerationFailures.TryGetValue(directory, out var error))
            {
                throw new StorageProviderException(error, "Configured enumeration failure.");
            }

            if (!_children.TryGetValue(directory, out var children))
            {
                yield break;
            }

            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return child;
            }
        }

        public ValueTask<Stream> OpenReadAsync(
            StoragePath path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<IStorageWriteSession> BeginWriteAsync(
            StoragePath path,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask CreateDirectoryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask MoveAsync(
            StoragePath source,
            StoragePath destination,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DeleteAsync(
            StoragePath path,
            StorageDeleteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
