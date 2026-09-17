namespace Foldara.Engine;

/// <summary>
/// Contains provider-neutral error information safe for diagnostics and operation history.
/// </summary>
public sealed class SynchronizationErrorDetails
{
    public SynchronizationErrorDetails(
        SynchronizationErrorCode code,
        string message,
        bool isRetryable)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown synchronization error code.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        IsRetryable = isRetryable;
    }

    public SynchronizationErrorCode Code { get; }

    public string Message { get; }

    public bool IsRetryable { get; }
}
