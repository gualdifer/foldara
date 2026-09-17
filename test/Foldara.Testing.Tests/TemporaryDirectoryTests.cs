using System.Text;
using Foldara.Core;
using Xunit;

namespace Foldara.Testing.Tests;

public sealed class TemporaryDirectoryTests
{
    [Fact]
    public void CreateUsesAnIsolatedDirectoryAndDisposeRemovesIt()
    {
        string rootPath;

        using (var directory = TemporaryDirectory.Create())
        {
            rootPath = directory.RootPath;
            Assert.True(Directory.Exists(rootPath));
        }

        Assert.False(Directory.Exists(rootPath));
    }

    [Fact]
    public void CreateUsesAUniqueRootForEveryFixture()
    {
        using var first = TemporaryDirectory.Create();
        using var second = TemporaryDirectory.Create();

        Assert.NotEqual(first.RootPath, second.RootPath);
    }

    [Fact]
    public void FluentMethodsBuildANestedDirectoryTree()
    {
        using var directory = TemporaryDirectory.Create()
            .CreateDirectory(StoragePath.Parse("empty"))
            .WriteTextFile(StoragePath.Parse("documents/readme.txt"), "hello")
            .WriteFile(StoragePath.Parse("data/empty.bin"), ReadOnlySpan<byte>.Empty);

        Assert.Equal(
            ["data/", "data/empty.bin", "documents/", "documents/readme.txt", "empty/"],
            directory.GetTreeSnapshot());
        Assert.Equal(
            "hello",
            File.ReadAllText(
                directory.GetFullPath(StoragePath.Parse("documents/readme.txt")),
                Encoding.UTF8));
    }

    [Fact]
    public void WriteFileRejectsTheFixtureRoot()
    {
        using var directory = TemporaryDirectory.Create();

        Assert.Throws<ArgumentException>(() =>
            directory.WriteFile(StoragePath.Root, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void WriteFileRejectsNullPath()
    {
        using var directory = TemporaryDirectory.Create();

        Assert.Throws<ArgumentNullException>(() =>
            directory.WriteFile(null!, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void OperationsRejectUseAfterDispose()
    {
        var directory = TemporaryDirectory.Create();
        directory.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            directory.GetFullPath(StoragePath.Parse("file.txt")));
    }
}
