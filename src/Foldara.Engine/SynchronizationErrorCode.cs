namespace Foldara.Engine;

/// <summary>
/// Classifies sanitized synchronization failures without provider-specific details.
/// </summary>
public enum SynchronizationErrorCode
{
    PlanConflict,
    UnsupportedMove,
    DestinationConflict,
    SourceChanged,
    MoveSourceMissing,
    ProviderUnknown,
    ProviderNotFound,
    ProviderAlreadyExists,
    ProviderAccessDenied,
    ProviderInvalidPath,
    ProviderUnsupportedOperation,
    ProviderConflict,
    ProviderRateLimited,
    ProviderOffline,
    ProviderTransientFailure,
    ProviderIntegrityFailure,
    Cancelled,
    UnexpectedFailure,
}
