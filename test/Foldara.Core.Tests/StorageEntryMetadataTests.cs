using Xunit;

namespace Foldara.Core.Tests;

public sealed class StorageEntryMetadataTests
{
    [Fact]
    public void ConstructorRepresentsProviderNeutralMetadata()
    {
        var createdAt = new DateTimeOffset(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);
        var lastModifiedAt = createdAt.AddMinutes(5);
        var hash = new StorageContentHash("SHA-256", "abc123");
        var properties = new Dictionary<string, string>
        {
            ["mimeType"] = "application/pdf",
        };

        var metadata = new StorageEntryMetadata(
            size: 42,
            createdAt: createdAt,
            lastModifiedAt: lastModifiedAt,
            providerItemId: "provider-id",
            contentHash: hash,
            revision: "revision-7",
            providerProperties: properties);

        Assert.Equal(42, metadata.Size);
        Assert.Equal(createdAt, metadata.CreatedAt);
        Assert.Equal(lastModifiedAt, metadata.LastModifiedAt);
        Assert.Equal("provider-id", metadata.ProviderItemId);
        Assert.Same(hash, metadata.ContentHash);
        Assert.Equal("revision-7", metadata.Revision);
        Assert.Equal("application/pdf", metadata.ProviderProperties["mimeType"]);
    }

    [Fact]
    public void ConstructorDefensivelyCopiesProviderProperties()
    {
        var properties = new Dictionary<string, string>
        {
            ["mimeType"] = "text/plain",
        };
        var metadata = new StorageEntryMetadata(providerProperties: properties);

        properties["mimeType"] = "application/octet-stream";
        properties["newProperty"] = "newValue";

        Assert.Equal("text/plain", metadata.ProviderProperties["mimeType"]);
        Assert.False(metadata.ProviderProperties.ContainsKey("newProperty"));
    }

    [Fact]
    public void EmptyMetadataHasNoProviderProperties()
    {
        var metadata = new StorageEntryMetadata();

        Assert.Empty(metadata.ProviderProperties);
    }

    [Fact]
    public void ConstructorRejectsNegativeSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StorageEntryMetadata(size: -1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" value")]
    [InlineData("value ")]
    public void ConstructorRejectsInvalidProviderItemId(string value)
    {
        Assert.Throws<ArgumentException>(() => new StorageEntryMetadata(providerItemId: value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" value")]
    [InlineData("value ")]
    public void ConstructorRejectsInvalidRevision(string value)
    {
        Assert.Throws<ArgumentException>(() => new StorageEntryMetadata(revision: value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" key")]
    [InlineData("key ")]
    public void ConstructorRejectsInvalidProviderPropertyKey(string key)
    {
        var properties = new Dictionary<string, string> { [key] = "value" };

        Assert.Throws<ArgumentException>(() =>
            new StorageEntryMetadata(providerProperties: properties));
    }
}
