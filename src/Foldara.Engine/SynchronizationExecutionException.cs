using Foldara.Core;

namespace Foldara.Engine;

/// <summary>
/// Represents a plan validation or execution failure outside a provider operation.
/// </summary>
public sealed class SynchronizationExecutionException : Exception
{
    public SynchronizationExecutionException(
        SynchronizationOperation operation,
        SynchronizationErrorCode errorCode,
        string message,
        Exception? innerException = null,
        bool isRetryable = false)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!Enum.IsDefined(errorCode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorCode),
                errorCode,
                "Unknown synchronization error code.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Operation = operation;
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }

    public SynchronizationOperation Operation { get; }

    public SynchronizationErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets a value indicating whether retrying after refreshing the plan may succeed.
    /// </summary>
    public bool IsRetryable { get; }
}
