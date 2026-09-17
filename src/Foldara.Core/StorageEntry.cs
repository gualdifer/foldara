namespace Foldara.Core;

/// <summary>
/// Represents a file or directory observed beneath a provider storage root.
/// </summary>
public sealed class StorageEntry
{
    public StorageEntry(
        StoragePath path,
        StorageEntryKind kind,
        StorageEntryMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown storage entry kind.");
        }

        metadata ??= new StorageEntryMetadata();

        if (kind == StorageEntryKind.Directory && metadata.Size is not null)
        {
            throw new ArgumentException("A directory cannot have a file size.", nameof(metadata));
        }

        if (kind == StorageEntryKind.Directory && metadata.ContentHash is not null)
        {
            throw new ArgumentException("A directory cannot have a content hash.", nameof(metadata));
        }

        Path = path;
        Kind = kind;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the provider-independent path relative to the configured storage root.
    /// </summary>
    public StoragePath Path { get; }

    /// <summary>
    /// Gets the kind of entry.
    /// </summary>
    public StorageEntryKind Kind { get; }

    /// <summary>
    /// Gets the metadata observed for the entry.
    /// </summary>
    public StorageEntryMetadata Metadata { get; }
}
