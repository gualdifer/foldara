namespace Foldara.Engine;

/// <summary>
/// Describes the outcome of one planned synchronization operation.
/// </summary>
public enum SynchronizationOperationOutcome
{
    Succeeded,
    Skipped,
    Failed,
    Cancelled,
    NotRun,
}
