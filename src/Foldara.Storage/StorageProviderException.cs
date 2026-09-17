namespace Foldara.Storage;

/// <summary>
/// Represents a sanitized, provider-neutral failure from a storage operation.
/// </summary>
public sealed class StorageProviderException : Exception
{
    public StorageProviderException(
        StorageProviderError error,
        string message,
        Exception? innerException = null,
        TimeSpan? retryAfter = null)
        : base(message, innerException)
    {
        if (!Enum.IsDefined(error))
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown provider error.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                retryAfter,
                "A retry delay cannot be negative.");
        }

        Error = error;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the provider-neutral failure classification.
    /// </summary>
    public StorageProviderError Error { get; }

    /// <summary>
    /// Gets the provider-requested retry delay, when one was supplied.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets a value indicating whether retrying later may succeed without changing the operation.
    /// </summary>
    public bool IsRetryable =>
        Error is StorageProviderError.RateLimited
            or StorageProviderError.Offline
            or StorageProviderError.TransientFailure;
}
