using Foldara.Core;

namespace Foldara.Engine;

/// <summary>
/// Records the immutable outcome of one planned operation.
/// </summary>
public sealed class SynchronizationOperationResult
{
    public SynchronizationOperationResult(
        Guid runId,
        Guid operationId,
        SynchronizationOperation operation,
        TimeSpan duration,
        SynchronizationOperationOutcome outcome,
        SynchronizationErrorDetails? error = null)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A run identifier cannot be empty.", nameof(runId));
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An operation identifier cannot be empty.", nameof(operationId));
        }

        ArgumentNullException.ThrowIfNull(operation);

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A duration cannot be negative.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown operation outcome.");
        }

        var requiresError = outcome is SynchronizationOperationOutcome.Failed
            or SynchronizationOperationOutcome.Cancelled;

        if (requiresError != (error is not null))
        {
            throw new ArgumentException(
                "Failed and cancelled operations require error details; other outcomes cannot have them.",
                nameof(error));
        }

        RunId = runId;
        OperationId = operationId;
        Operation = operation;
        Duration = duration;
        Outcome = outcome;
        Error = error;
    }

    public Guid RunId { get; }

    public Guid OperationId { get; }

    public SynchronizationOperation Operation { get; }

    public TimeSpan Duration { get; }

    public SynchronizationOperationOutcome Outcome { get; }

    public SynchronizationErrorDetails? Error { get; }
}
