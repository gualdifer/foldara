namespace Foldara.Storage;

/// <summary>
/// Controls whether a deleted entry must remain recoverable through provider trash.
/// </summary>
public enum StorageDeleteMode
{
    Permanent,
    Trash,
}
