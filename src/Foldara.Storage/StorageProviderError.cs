namespace Foldara.Storage;

/// <summary>
/// Classifies provider failures without exposing provider SDK exception types.
/// </summary>
public enum StorageProviderError
{
    Unknown,
    NotFound,
    AlreadyExists,
    AccessDenied,
    InvalidPath,
    UnsupportedOperation,
    Conflict,
    RateLimited,
    Offline,
    TransientFailure,
    IntegrityFailure,
}
