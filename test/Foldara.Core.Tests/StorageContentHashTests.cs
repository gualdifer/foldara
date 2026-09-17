using Xunit;

namespace Foldara.Core.Tests;

public sealed class StorageContentHashTests
{
    [Fact]
    public void ConstructorPreservesAlgorithmAndProviderEncoding()
    {
        var hash = new StorageContentHash("SHA-256", "AbC+/=");

        Assert.Equal("SHA-256", hash.Algorithm);
        Assert.Equal("AbC+/=", hash.Value);
    }

    [Theory]
    [InlineData(null, "value")]
    [InlineData("", "value")]
    [InlineData(" ", "value")]
    [InlineData(" SHA-256", "value")]
    [InlineData("SHA-256 ", "value")]
    [InlineData("SHA-256", null)]
    [InlineData("SHA-256", "")]
    [InlineData("SHA-256", " ")]
    [InlineData("SHA-256", " value")]
    [InlineData("SHA-256", "value ")]
    public void ConstructorRejectsInvalidValues(string? algorithm, string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new StorageContentHash(algorithm!, value!));
    }
}
