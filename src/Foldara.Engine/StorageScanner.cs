using Foldara.Core;
using Foldara.Storage;

namespace Foldara.Engine;

/// <summary>
/// Recursively scans provider entries into deterministic snapshots.
/// </summary>
public sealed class StorageScanner
{
    public async ValueTask<StorageSnapshot> ScanAsync(
        IStorageProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        cancellationToken.ThrowIfCancellationRequested();

        var observedEntries = new List<StorageEntry>();
        var pendingDirectories = new Queue<StoragePath>();
        pendingDirectories.Enqueue(StoragePath.Root);

        while (pendingDirectories.TryDequeue(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<StorageEntry> children;

            try
            {
                children = await EnumerateChildrenAsync(
                    provider,
                    directory,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (StorageProviderException exception) when (
                directory != StoragePath.Root && exception.Error == StorageProviderError.NotFound)
            {
                observedEntries.RemoveAll(entry => entry.Path == directory);
                continue;
            }

            foreach (var child in children)
            {
                ValidateDirectChild(directory, child);
                observedEntries.Add(child);

                if (child.Kind == StorageEntryKind.Directory)
                {
                    pendingDirectories.Enqueue(child.Path);
                }
            }
        }

        try
        {
            return new StorageSnapshot(
                provider.Capabilities.PathCaseSensitivity,
                observedEntries);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The provider returned entries that cannot form a valid snapshot.",
                exception);
        }
    }

    private static async ValueTask<IReadOnlyList<StorageEntry>> EnumerateChildrenAsync(
        IStorageProvider provider,
        StoragePath directory,
        CancellationToken cancellationToken)
    {
        var children = new List<StorageEntry>();

        await foreach (var child in provider
            .EnumerateChildrenAsync(directory, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            children.Add(child);
        }

        return children;
    }

    private static void ValidateDirectChild(StoragePath directory, StorageEntry? child)
    {
        if (child is null)
        {
            throw new InvalidOperationException("Provider returned a null storage entry.");
        }

        if (child.Path.IsRoot || child.Path.Parent != directory)
        {
            throw new InvalidOperationException(
                $"Provider returned '{child.Path.Value}' while enumerating direct children of " +
                $"'{directory.Value}'.");
        }
    }
}
