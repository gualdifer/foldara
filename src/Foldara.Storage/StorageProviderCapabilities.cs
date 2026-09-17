namespace Foldara.Storage;

/// <summary>
/// Describes behavior that an engine may rely on for one configured provider root.
/// </summary>
public sealed class StorageProviderCapabilities
{
    public StorageProviderCapabilities(
        StoragePathCaseSensitivity pathCaseSensitivity,
        bool hasStableItemIdentifiers = false,
        bool supportsMove = false,
        bool supportsAtomicMove = false,
        bool supportsAtomicReplace = false,
        bool supportsDelete = false,
        bool supportsTrash = false,
        bool supportsChangeFeed = false,
        bool supportsResumableWrites = false,
        bool providesContentHashes = false,
        bool supportsVersioning = false)
    {
        if (!Enum.IsDefined(pathCaseSensitivity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pathCaseSensitivity),
                pathCaseSensitivity,
                "Unknown path case-sensitivity behavior.");
        }

        if (supportsAtomicMove && !supportsMove)
        {
            throw new ArgumentException(
                "Atomic move support requires move support.",
                nameof(supportsAtomicMove));
        }

        if (supportsTrash && !supportsDelete)
        {
            throw new ArgumentException(
                "Trash support requires delete support.",
                nameof(supportsTrash));
        }

        PathCaseSensitivity = pathCaseSensitivity;
        HasStableItemIdentifiers = hasStableItemIdentifiers;
        SupportsMove = supportsMove;
        SupportsAtomicMove = supportsAtomicMove;
        SupportsAtomicReplace = supportsAtomicReplace;
        SupportsDelete = supportsDelete;
        SupportsTrash = supportsTrash;
        SupportsChangeFeed = supportsChangeFeed;
        SupportsResumableWrites = supportsResumableWrites;
        ProvidesContentHashes = providesContentHashes;
        SupportsVersioning = supportsVersioning;
    }

    public StoragePathCaseSensitivity PathCaseSensitivity { get; }

    public bool HasStableItemIdentifiers { get; }

    public bool SupportsMove { get; }

    public bool SupportsAtomicMove { get; }

    public bool SupportsAtomicReplace { get; }

    public bool SupportsDelete { get; }

    public bool SupportsTrash { get; }

    public bool SupportsChangeFeed { get; }

    public bool SupportsResumableWrites { get; }

    public bool ProvidesContentHashes { get; }

    public bool SupportsVersioning { get; }
}
