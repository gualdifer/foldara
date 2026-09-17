using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Foldara.Core;
using Foldara.Storage;

namespace Foldara.Engine;

/// <summary>
/// Contains a deterministic, immutable observation of entries beneath a storage root.
/// </summary>
public sealed class StorageSnapshot
{
    private readonly IReadOnlyDictionary<StoragePath, StorageEntry> _entriesByPath;

    public StorageSnapshot(
        StoragePathCaseSensitivity pathCaseSensitivity,
        IEnumerable<StorageEntry> entries)
    {
        if (!Enum.IsDefined(pathCaseSensitivity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pathCaseSensitivity),
                pathCaseSensitivity,
                "Unknown path case-sensitivity behavior.");
        }

        ArgumentNullException.ThrowIfNull(entries);

        var pathComparer = StoragePathComparer.For(pathCaseSensitivity);
        var entriesByPath = new Dictionary<StoragePath, StorageEntry>(pathComparer);

        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new ArgumentException("A snapshot cannot contain a null entry.", nameof(entries));
            }

            if (entry.Path.IsRoot)
            {
                throw new ArgumentException(
                    "The storage root is the snapshot container and cannot be an entry.",
                    nameof(entries));
            }

            if (!entriesByPath.TryAdd(entry.Path, entry))
            {
                throw new ArgumentException(
                    $"The snapshot contains duplicate path '{entry.Path.Value}'.",
                    nameof(entries));
            }
        }

        var orderedEntries = entriesByPath.Values
            .OrderBy(entry => entry.Path, pathComparer)
            .ToArray();

        PathCaseSensitivity = pathCaseSensitivity;
        Entries = new ReadOnlyCollection<StorageEntry>(orderedEntries);
        _entriesByPath = new ReadOnlyDictionary<StoragePath, StorageEntry>(entriesByPath);
    }

    public StoragePathCaseSensitivity PathCaseSensitivity { get; }

    /// <summary>
    /// Gets entries ordered by normalized path using provider path semantics.
    /// </summary>
    public IReadOnlyList<StorageEntry> Entries { get; }

    public bool TryGetEntry(
        StoragePath path,
        [NotNullWhen(true)] out StorageEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _entriesByPath.TryGetValue(path, out entry);
    }

    private sealed class StoragePathComparer : IComparer<StoragePath>, IEqualityComparer<StoragePath>
    {
        private readonly StringComparer _equalityComparer;
        private readonly StringComparer _orderingComparer;

        private StoragePathComparer(StoragePathCaseSensitivity pathCaseSensitivity)
        {
            _equalityComparer = pathCaseSensitivity == StoragePathCaseSensitivity.Insensitive
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            _orderingComparer = _equalityComparer;
        }

        public static StoragePathComparer For(StoragePathCaseSensitivity pathCaseSensitivity) =>
            new(pathCaseSensitivity);

        public int Compare(StoragePath? x, StoragePath? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            return _orderingComparer.Compare(x.Value, y.Value);
        }

        public bool Equals(StoragePath? x, StoragePath? y) =>
            ReferenceEquals(x, y) ||
            (x is not null && y is not null && _equalityComparer.Equals(x.Value, y.Value));

        public int GetHashCode(StoragePath obj) => _equalityComparer.GetHashCode(obj.Value);
    }
}
