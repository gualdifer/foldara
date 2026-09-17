using Foldara.Core;
using Foldara.Storage;

namespace Foldara.Engine;

/// <summary>
/// Builds deterministic, non-destructive plans from source and destination snapshots.
/// </summary>
public sealed class OneWaySynchronizationPlanner
{
    public SynchronizationPlan Plan(
        StorageSnapshot source,
        StorageSnapshot destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        var destinationPathComparer = GetPathComparer(destination.PathCaseSensitivity);
        var ambiguousSourcePaths = FindAmbiguousSourcePaths(source, destinationPathComparer);
        var matchedDestinationPaths = new HashSet<string>(destinationPathComparer);
        var operations = new List<SynchronizationOperation>();

        foreach (var sourceEntry in source.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            destination.TryGetEntry(
                sourceEntry.Path,
                out var destinationEntry);

            if (destinationEntry is not null)
            {
                matchedDestinationPaths.Add(destinationEntry.Path.Value);
            }

            if (ambiguousSourcePaths.Contains(sourceEntry.Path))
            {
                operations.Add(SynchronizationOperation.Conflict(
                    sourceEntry.Path,
                    destinationEntry?.Path ?? sourceEntry.Path,
                    "Multiple source paths map to the same destination path."));
                continue;
            }

            operations.Add(CreateOperation(sourceEntry, destinationEntry));
        }

        for (var index = destination.Entries.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationEntry = destination.Entries[index];

            if (!matchedDestinationPaths.Contains(destinationEntry.Path.Value))
            {
                operations.Add(SynchronizationOperation.DeleteCandidate(
                    destinationEntry.Path,
                    "Destination entry has no source counterpart; deletion is disabled."));
            }
        }

        return new SynchronizationPlan(operations);
    }

    private static SynchronizationOperation CreateOperation(
        StorageEntry source,
        StorageEntry? destination)
    {
        if (destination is null)
        {
            return source.Kind == StorageEntryKind.Directory
                ? SynchronizationOperation.CreateDirectory(
                    source.Path,
                    "Source directory is missing from the destination.")
                : SynchronizationOperation.Copy(
                    source.Path,
                    source.Path,
                    "Source file is missing from the destination.");
        }

        if (source.Kind != destination.Kind)
        {
            var reason = source.Kind == StorageEntryKind.File
                ? "Source file conflicts with a destination directory."
                : "Source directory conflicts with a destination file.";

            return SynchronizationOperation.Conflict(
                source.Path,
                destination.Path,
                reason);
        }

        if (source.Kind == StorageEntryKind.Directory)
        {
            return SynchronizationOperation.Skip(
                source.Path,
                destination.Path,
                "Source and destination directories already match.");
        }

        var comparison = CompareFiles(source.Metadata, destination.Metadata);

        return comparison.AreEqual
            ? SynchronizationOperation.Skip(
                source.Path,
                destination.Path,
                comparison.Reason)
            : SynchronizationOperation.Replace(
                source.Path,
                destination.Path,
                comparison.Reason);
    }

    private static FileComparison CompareFiles(
        StorageEntryMetadata source,
        StorageEntryMetadata destination)
    {
        if (source.Size is not null &&
            destination.Size is not null &&
            source.Size != destination.Size)
        {
            return FileComparison.Different("File sizes differ.");
        }

        if (CanCompareHashes(source.ContentHash, destination.ContentHash))
        {
            return string.Equals(
                source.ContentHash!.Value,
                destination.ContentHash!.Value,
                StringComparison.Ordinal)
                ? FileComparison.Equal("File content hash matches.")
                : FileComparison.Different("File content hashes differ.");
        }

        if (source.Size is not null &&
            destination.Size is not null &&
            source.LastModifiedAt is not null &&
            destination.LastModifiedAt is not null)
        {
            return source.LastModifiedAt == destination.LastModifiedAt
                ? FileComparison.Equal("File size and last-modified timestamp match.")
                : FileComparison.Different("File last-modified timestamps differ.");
        }

        return FileComparison.Different(
            "File metadata is insufficient to prove that content matches.");
    }

    private static bool CanCompareHashes(
        StorageContentHash? source,
        StorageContentHash? destination) =>
        source is not null &&
        destination is not null &&
        string.Equals(source.Algorithm, destination.Algorithm, StringComparison.OrdinalIgnoreCase);

    private static HashSet<StoragePath> FindAmbiguousSourcePaths(
        StorageSnapshot source,
        StringComparer destinationPathComparer)
    {
        var pathsByDestination = new Dictionary<string, List<StoragePath>>(destinationPathComparer);

        foreach (var entry in source.Entries)
        {
            if (!pathsByDestination.TryGetValue(entry.Path.Value, out var paths))
            {
                paths = [];
                pathsByDestination.Add(entry.Path.Value, paths);
            }

            paths.Add(entry.Path);
        }

        return pathsByDestination.Values
            .Where(paths => paths.Count > 1)
            .SelectMany(paths => paths)
            .ToHashSet();
    }

    private static StringComparer GetPathComparer(
        StoragePathCaseSensitivity pathCaseSensitivity) =>
        pathCaseSensitivity == StoragePathCaseSensitivity.Insensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed record FileComparison(bool AreEqual, string Reason)
    {
        public static FileComparison Equal(string reason) => new(true, reason);

        public static FileComparison Different(string reason) => new(false, reason);
    }
}
