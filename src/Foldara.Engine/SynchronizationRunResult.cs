using System.Collections.ObjectModel;

namespace Foldara.Engine;

/// <summary>
/// Records the immutable terminal result of one synchronization run.
/// </summary>
public sealed class SynchronizationRunResult
{
    public SynchronizationRunResult(
        Guid runId,
        TimeSpan duration,
        SynchronizationRunOutcome outcome,
        IEnumerable<SynchronizationOperationResult> operations,
        SynchronizationErrorDetails? error = null)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A run identifier cannot be empty.", nameof(runId));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A duration cannot be negative.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown run outcome.");
        }

        ArgumentNullException.ThrowIfNull(operations);

        var operationCopy = operations.ToArray();

        if (operationCopy.Any(operation => operation is null || operation.RunId != runId))
        {
            throw new ArgumentException(
                "Every operation result must be non-null and belong to the run.",
                nameof(operations));
        }

        if (operationCopy.Select(operation => operation.OperationId).Distinct().Count() !=
            operationCopy.Length)
        {
            throw new ArgumentException(
                "Operation identifiers must be unique within a run.",
                nameof(operations));
        }

        var requiresError = outcome is SynchronizationRunOutcome.Failed
            or SynchronizationRunOutcome.Cancelled;

        if (requiresError != (error is not null))
        {
            throw new ArgumentException(
                "Failed and cancelled runs require error details; successful runs cannot have them.",
                nameof(error));
        }

        RunId = runId;
        Duration = duration;
        Outcome = outcome;
        Operations = new ReadOnlyCollection<SynchronizationOperationResult>(operationCopy);
        Error = error;
    }

    public Guid RunId { get; }

    public TimeSpan Duration { get; }

    public SynchronizationRunOutcome Outcome { get; }

    public IReadOnlyList<SynchronizationOperationResult> Operations { get; }

    public SynchronizationErrorDetails? Error { get; }
}
