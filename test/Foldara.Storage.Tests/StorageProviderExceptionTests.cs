using Xunit;

namespace Foldara.Storage.Tests;

public sealed class StorageProviderExceptionTests
{
    [Theory]
    [InlineData(StorageProviderError.RateLimited)]
    [InlineData(StorageProviderError.Offline)]
    [InlineData(StorageProviderError.TransientFailure)]
    public void RetryableErrorsAreIdentified(StorageProviderError error)
    {
        var exception = new StorageProviderException(error, "Safe diagnostic message.");

        Assert.True(exception.IsRetryable);
    }

    [Theory]
    [InlineData(StorageProviderError.Unknown)]
    [InlineData(StorageProviderError.NotFound)]
    [InlineData(StorageProviderError.AlreadyExists)]
    [InlineData(StorageProviderError.AccessDenied)]
    [InlineData(StorageProviderError.InvalidPath)]
    [InlineData(StorageProviderError.UnsupportedOperation)]
    [InlineData(StorageProviderError.Conflict)]
    [InlineData(StorageProviderError.IntegrityFailure)]
    public void NonRetryableErrorsAreIdentified(StorageProviderError error)
    {
        var exception = new StorageProviderException(error, "Safe diagnostic message.");

        Assert.False(exception.IsRetryable);
    }

    [Fact]
    public void ConstructorPreservesDiagnosticDetails()
    {
        var innerException = new IOException("Provider-specific failure.");
        var retryAfter = TimeSpan.FromSeconds(10);

        var exception = new StorageProviderException(
            StorageProviderError.RateLimited,
            "The provider rate limit was reached.",
            innerException,
            retryAfter);

        Assert.Equal(StorageProviderError.RateLimited, exception.Error);
        Assert.Equal("The provider rate limit was reached.", exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(retryAfter, exception.RetryAfter);
    }

    [Fact]
    public void ConstructorRejectsUnknownError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StorageProviderException((StorageProviderError)42, "Message."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorRejectsInvalidMessage(string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new StorageProviderException(StorageProviderError.Unknown, message!));
    }

    [Fact]
    public void ConstructorRejectsNegativeRetryDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StorageProviderException(
                StorageProviderError.TransientFailure,
                "Temporary failure.",
                retryAfter: TimeSpan.FromSeconds(-1)));
    }
}
