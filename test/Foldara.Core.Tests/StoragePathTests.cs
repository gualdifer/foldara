using Xunit;

namespace Foldara.Core.Tests;

public sealed class StoragePathTests
{
    [Fact]
    public void ParseEmptyStringReturnsRoot()
    {
        var path = StoragePath.Parse(string.Empty);

        Assert.Same(StoragePath.Root, path);
        Assert.True(path.IsRoot);
        Assert.Equal(string.Empty, path.Name);
        Assert.Null(path.Parent);
    }

    [Fact]
    public void ParseRelativePathPreservesProviderIndependentRepresentation()
    {
        var path = StoragePath.Parse("Documents/Invoices/2026.pdf");

        Assert.Equal("Documents/Invoices/2026.pdf", path.Value);
        Assert.Equal("2026.pdf", path.Name);
        Assert.Equal(StoragePath.Parse("Documents/Invoices"), path.Parent);
        Assert.False(path.IsRoot);
        Assert.Equal(path.Value, path.ToString());
    }

    [Fact]
    public void ParentOfSingleSegmentIsRoot()
    {
        var path = StoragePath.Parse("Documents");

        Assert.Same(StoragePath.Root, path.Parent);
    }

    [Fact]
    public void AppendBuildsAChildPath()
    {
        var path = StoragePath.Root
            .Append("Documents")
            .Append("Invoices")
            .Append("2026.pdf");

        Assert.Equal(StoragePath.Parse("Documents/Invoices/2026.pdf"), path);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../secret.txt")]
    [InlineData("folder/../secret.txt")]
    [InlineData("folder/./file.txt")]
    [InlineData("folder//file.txt")]
    [InlineData("folder/")]
    [InlineData("folder\\file.txt")]
    [InlineData("folder/\0/file.txt")]
    public void ParseRejectsInvalidPaths(string value)
    {
        Assert.Throws<FormatException>(() => StoragePath.Parse(value));
    }

    [Fact]
    public void ParseRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => StoragePath.Parse(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Documents")]
    [InlineData("Documents/file.txt")]
    [InlineData("résumé/日本語.txt")]
    public void TryParseAcceptsValidPaths(string value)
    {
        var succeeded = StoragePath.TryParse(value, out var path);

        Assert.True(succeeded);
        Assert.NotNull(path);
        Assert.Equal(value, path.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/absolute")]
    [InlineData("parent/../file")]
    public void TryParseRejectsInvalidPaths(string? value)
    {
        var succeeded = StoragePath.TryParse(value, out var path);

        Assert.False(succeeded);
        Assert.Null(path);
    }

    [Fact]
    public void EqualityIsOrdinalAndCaseSensitive()
    {
        var lowerCase = StoragePath.Parse("documents/file.txt");
        var upperCase = StoragePath.Parse("Documents/file.txt");

        Assert.NotEqual(lowerCase, upperCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nested/segment")]
    [InlineData("nested\\segment")]
    [InlineData("bad\0segment")]
    public void AppendRejectsInvalidSegments(string segment)
    {
        Assert.Throws<FormatException>(() => StoragePath.Root.Append(segment));
    }

    [Fact]
    public void AppendRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => StoragePath.Root.Append(null!));
    }
}
