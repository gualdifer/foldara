namespace Foldara.Storage;

/// <summary>
/// Describes how a provider compares path segments.
/// </summary>
public enum StoragePathCaseSensitivity
{
    Sensitive,
    Insensitive,
    ProviderDependent,
}
