using System.Text;
using Foldara.Core;
using Foldara.Testing;
using Xunit;

namespace Foldara.Storage.Local.Tests;

public sealed class LocalStorageProviderTests
{
    [Fact]
    public void ConstructorRejectsMissingRoot()
    {
        using var directory = TemporaryDirectory.Create();
        var missingPath = Path.Combine(directory.RootPath, "missing");

        Assert.Throws<DirectoryNotFoundException>(() => new LocalStorageProvider(missingPath));
    }

    [Fact]
    public void CapabilitiesAreConservativeAndPlatformAware()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);

        var expectedCaseSensitivity = OperatingSystem.IsWindows()
            ? StoragePathCaseSensitivity.Insensitive
            : OperatingSystem.IsMacOS()
                ? StoragePathCaseSensitivity.ProviderDependent
                : StoragePathCaseSensitivity.Sensitive;

        Assert.Equal(expectedCaseSensitivity, provider.Capabilities.PathCaseSensitivity);
        Assert.True(provider.Capabilities.SupportsMove);
        Assert.True(provider.Capabilities.SupportsDelete);
        Assert.False(provider.Capabilities.SupportsAtomicMove);
        Assert.False(provider.Capabilities.SupportsAtomicReplace);
        Assert.False(provider.Capabilities.SupportsTrash);
        Assert.False(provider.Capabilities.HasStableItemIdentifiers);
        Assert.False(provider.Capabilities.ProvidesContentHashes);
    }

    [Fact]
    public async Task GetEntryMapsFilesAndDirectories()
    {
        using var directory = TemporaryDirectory.Create()
            .CreateDirectory(StoragePath.Parse("documents"))
            .WriteTextFile(StoragePath.Parse("documents/file.txt"), "hello");
        var provider = new LocalStorageProvider(directory.RootPath);
        var cancellationToken = TestContext.Current.CancellationToken;

        var folder = await provider.GetEntryAsync(StoragePath.Parse("documents"), cancellationToken);
        var file = await provider.GetEntryAsync(
            StoragePath.Parse("documents/file.txt"),
            cancellationToken);

        Assert.NotNull(folder);
        Assert.Equal(StorageEntryKind.Directory, folder.Kind);
        Assert.Null(folder.Metadata.Size);
        Assert.NotNull(folder.Metadata.LastModifiedAt);
        Assert.NotNull(file);
        Assert.Equal(StorageEntryKind.File, file.Kind);
        Assert.Equal(5, file.Metadata.Size);
        Assert.NotNull(file.Metadata.CreatedAt);
        Assert.NotNull(file.Metadata.LastModifiedAt);
        Assert.Null(file.Metadata.ProviderItemId);
        Assert.Null(file.Metadata.ContentHash);
        Assert.Null(file.Metadata.Revision);
    }

    [Fact]
    public async Task GetEntryReturnsNullForMissingPath()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);

        var entry = await provider.GetEntryAsync(
            StoragePath.Parse("missing.txt"),
            TestContext.Current.CancellationToken);

        Assert.Null(entry);
    }

    [Fact]
    public async Task EnumerateChildrenReturnsOnlyDirectChildren()
    {
        using var directory = TemporaryDirectory.Create()
            .CreateDirectory(StoragePath.Parse("empty"))
            .WriteTextFile(StoragePath.Parse("root.txt"), "root")
            .WriteTextFile(StoragePath.Parse("nested/child.txt"), "child");
        var provider = new LocalStorageProvider(directory.RootPath);

        var entries = await EnumerateAsync(
            provider,
            StoragePath.Root,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, entry => entry.Path == StoragePath.Parse("empty"));
        Assert.Contains(entries, entry => entry.Path == StoragePath.Parse("root.txt"));
        Assert.Contains(entries, entry => entry.Path == StoragePath.Parse("nested"));
        Assert.DoesNotContain(entries, entry => entry.Path == StoragePath.Parse("nested/child.txt"));
    }

    [Fact]
    public async Task OpenReadStreamsFileContents()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "hello");
        var provider = new LocalStorageProvider(directory.RootPath);
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var stream = await provider.OpenReadAsync(
            StoragePath.Parse("file.txt"),
            cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Equal("hello", await reader.ReadToEndAsync(cancellationToken));
    }

    [Fact]
    public async Task WriteSessionPublishesFileOnlyOnCommit()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);
        var path = StoragePath.Parse("file.txt");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = await provider.BeginWriteAsync(
            path,
            StorageWriteMode.CreateNew,
            cancellationToken))
        {
            await session.Content.WriteAsync("hello"u8.ToArray(), cancellationToken);

            Assert.Null(await provider.GetEntryAsync(path, cancellationToken));
            Assert.Empty(await EnumerateAsync(provider, StoragePath.Root, cancellationToken));

            await session.CommitAsync(cancellationToken);
        }

        Assert.Equal("hello", File.ReadAllText(directory.GetFullPath(path), Encoding.UTF8));
    }

    [Fact]
    public async Task DisposingUncommittedWriteRemovesStagingFile()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);
        var path = StoragePath.Parse("file.txt");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = await provider.BeginWriteAsync(
            path,
            StorageWriteMode.CreateNew,
            cancellationToken))
        {
            await session.Content.WriteAsync("partial"u8.ToArray(), cancellationToken);
        }

        Assert.Null(await provider.GetEntryAsync(path, cancellationToken));
        Assert.Empty(directory.GetTreeSnapshot());
    }

    [Fact]
    public async Task CreateNewDoesNotOverwriteExistingFile()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "original");
        var provider = new LocalStorageProvider(directory.RootPath);
        var path = StoragePath.Parse("file.txt");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = await provider.BeginWriteAsync(
            path,
            StorageWriteMode.CreateNew,
            cancellationToken);
        await session.Content.WriteAsync("replacement"u8.ToArray(), cancellationToken);

        var exception = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await session.CommitAsync(cancellationToken));

        Assert.Equal(StorageProviderError.AlreadyExists, exception.Error);
        Assert.Equal("original", File.ReadAllText(directory.GetFullPath(path), Encoding.UTF8));
    }

    [Fact]
    public async Task CreateOrReplaceReplacesExistingFile()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "original");
        var provider = new LocalStorageProvider(directory.RootPath);
        var path = StoragePath.Parse("file.txt");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = await provider.BeginWriteAsync(
            path,
            StorageWriteMode.CreateOrReplace,
            cancellationToken))
        {
            await session.Content.WriteAsync("replacement"u8.ToArray(), cancellationToken);
            await session.CommitAsync(cancellationToken);
        }

        Assert.Equal("replacement", File.ReadAllText(directory.GetFullPath(path), Encoding.UTF8));
    }

    [Fact]
    public async Task CreateDirectoryIsIdempotentAndRejectsExistingFile()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "contents");
        var provider = new LocalStorageProvider(directory.RootPath);
        var folderPath = StoragePath.Parse("folder");
        var cancellationToken = TestContext.Current.CancellationToken;

        await provider.CreateDirectoryAsync(folderPath, cancellationToken);
        await provider.CreateDirectoryAsync(folderPath, cancellationToken);

        Assert.True(Directory.Exists(directory.GetFullPath(folderPath)));
        var exception = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await provider.CreateDirectoryAsync(StoragePath.Parse("file.txt"), cancellationToken));
        Assert.Equal(StorageProviderError.AlreadyExists, exception.Error);
    }

    [Fact]
    public async Task MoveAndDeleteOperateWithinTheRoot()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("source.txt"), "contents");
        var provider = new LocalStorageProvider(directory.RootPath);
        var source = StoragePath.Parse("source.txt");
        var destination = StoragePath.Parse("destination.txt");
        var cancellationToken = TestContext.Current.CancellationToken;

        await provider.MoveAsync(source, destination, StorageWriteMode.CreateNew, cancellationToken);

        Assert.Null(await provider.GetEntryAsync(source, cancellationToken));
        Assert.NotNull(await provider.GetEntryAsync(destination, cancellationToken));

        await provider.DeleteAsync(destination, StorageDeleteMode.Permanent, cancellationToken);
        await provider.DeleteAsync(destination, StorageDeleteMode.Permanent, cancellationToken);

        Assert.Null(await provider.GetEntryAsync(destination, cancellationToken));
    }

    [Fact]
    public async Task TrashAndRootDeletionAreRejected()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);
        var cancellationToken = TestContext.Current.CancellationToken;

        var trashException = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await provider.DeleteAsync(
                StoragePath.Parse("file.txt"),
                StorageDeleteMode.Trash,
                cancellationToken));
        var rootException = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await provider.DeleteAsync(
                StoragePath.Root,
                StorageDeleteMode.Permanent,
                cancellationToken));

        Assert.Equal(StorageProviderError.UnsupportedOperation, trashException.Error);
        Assert.Equal(StorageProviderError.InvalidPath, rootException.Error);
    }

    [Fact]
    public async Task DeletingNonEmptyDirectoryReportsConflict()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("folder/file.txt"), "contents");
        var provider = new LocalStorageProvider(directory.RootPath);

        var exception = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await provider.DeleteAsync(
                StoragePath.Parse("folder"),
                StorageDeleteMode.Permanent,
                TestContext.Current.CancellationToken));

        Assert.Equal(StorageProviderError.Conflict, exception.Error);
    }

    [Fact]
    public async Task OperationsHonorPreCancelledToken()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await provider.GetEntryAsync(StoragePath.Root, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await EnumerateAsync(provider, StoragePath.Root, cancellation.Token));
    }

    [Fact]
    public async Task SymbolicLinksAreRejectedInsteadOfFollowed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("target.txt"), "contents");
        var linkPath = directory.GetFullPath(StoragePath.Parse("link.txt"));
        File.CreateSymbolicLink(linkPath, "target.txt");
        var provider = new LocalStorageProvider(directory.RootPath);
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await provider.GetEntryAsync(StoragePath.Parse("link.txt"), cancellationToken));

        Assert.Equal(StorageProviderError.UnsupportedOperation, exception.Error);
    }

    [Fact]
    public async Task DiagnosticPathsEscapeControlCharacters()
    {
        using var directory = TemporaryDirectory.Create();
        var provider = new LocalStorageProvider(directory.RootPath);

        var exception = await Assert.ThrowsAsync<StorageProviderException>(async () =>
            await provider.OpenReadAsync(
                StoragePath.Parse("line\nbreak.txt"),
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain("\n", exception.Message, StringComparison.Ordinal);
        Assert.Contains("\\u000A", exception.Message, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<StorageEntry>> EnumerateAsync(
        IStorageProvider provider,
        StoragePath path,
        CancellationToken cancellationToken)
    {
        var entries = new List<StorageEntry>();

        await foreach (var entry in provider.EnumerateChildrenAsync(path, cancellationToken))
        {
            entries.Add(entry);
        }

        return entries;
    }
}
