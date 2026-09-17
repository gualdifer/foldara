using System.Runtime.CompilerServices;
using Foldara.Core;
using Foldara.Storage;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class SynchronizationDryRunServiceTests
{
    [Fact]
    public async Task CreatePlanReadsProvidersWithoutMutatingThem()
    {
        var source = new ReadOnlyProvider(
            new StorageEntry(
                StoragePath.Parse("file.txt"),
                StorageEntryKind.File,
                new StorageEntryMetadata(size: 5)));
        var destination = new ReadOnlyProvider();
        var service = new SynchronizationDryRunService();

        var plan = await service.CreatePlanAsync(
            source,
            destination,
            TestContext.Current.CancellationToken);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(SynchronizationOperationKind.Copy, operation.Kind);
        Assert.Equal(0, source.MutationAttempts);
        Assert.Equal(0, destination.MutationAttempts);
    }

    [Fact]
    public async Task CreatePlanHonorsPreCancelledToken()
    {
        var service = new SynchronizationDryRunService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.CreatePlanAsync(
                new ReadOnlyProvider(),
                new ReadOnlyProvider(),
                cancellation.Token));
    }

    private sealed class ReadOnlyProvider : IStorageProvider
    {
        private readonly IReadOnlyList<StorageEntry> _rootEntries;

        public ReadOnlyProvider(params StorageEntry[] rootEntries)
        {
            _rootEntries = rootEntries;
        }

        public StorageProviderCapabilities Capabilities { get; } =
            new(StoragePathCaseSensitivity.Sensitive);

        public int MutationAttempts { get; private set; }

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

            if (!directory.IsRoot)
            {
                yield break;
            }

            foreach (var entry in _rootEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entry;
            }
        }

        public ValueTask<Stream> OpenReadAsync(
            StoragePath path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<IStorageWriteSession> BeginWriteAsync(
            StoragePath path,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default)
        {
            MutationAttempts++;
            throw new InvalidOperationException("A dry run attempted to begin a write.");
        }

        public ValueTask CreateDirectoryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default)
        {
            MutationAttempts++;
            throw new InvalidOperationException("A dry run attempted to create a directory.");
        }

        public ValueTask MoveAsync(
            StoragePath source,
            StoragePath destination,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default)
        {
            MutationAttempts++;
            throw new InvalidOperationException("A dry run attempted to move an entry.");
        }

        public ValueTask DeleteAsync(
            StoragePath path,
            StorageDeleteMode mode,
            CancellationToken cancellationToken = default)
        {
            MutationAttempts++;
            throw new InvalidOperationException("A dry run attempted to delete an entry.");
        }
    }
}
