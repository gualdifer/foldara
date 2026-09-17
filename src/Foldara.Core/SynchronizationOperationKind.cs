namespace Foldara.Core;

/// <summary>
/// Identifies an action or decision recorded in a synchronization plan.
/// </summary>
public enum SynchronizationOperationKind
{
    Copy,
    CreateDirectory,
    Replace,
    Move,
    Skip,
    Conflict,
    DeleteCandidate,
}
