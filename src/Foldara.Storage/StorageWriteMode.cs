namespace Foldara.Storage;

/// <summary>
/// Controls how a write or move treats an existing destination entry.
/// </summary>
public enum StorageWriteMode
{
    CreateNew,
    CreateOrReplace,
}
