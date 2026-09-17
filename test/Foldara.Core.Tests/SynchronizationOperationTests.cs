using Xunit;

namespace Foldara.Core.Tests;

public sealed class SynchronizationOperationTests
{
    private static readonly StoragePath SourcePath = StoragePath.Parse("source/file.txt");
    private static readonly StoragePath DestinationPath = StoragePath.Parse("destination/file.txt");

    [Fact]
    public void CopyRepresentsSourceAndDestination()
    {
        var operation = SynchronizationOperation.Copy(SourcePath, DestinationPath, "Source is new.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.Copy,
            SourcePath,
            DestinationPath,
            "Source is new.");
    }

    [Fact]
    public void CreateDirectoryRepresentsDestinationOnly()
    {
        var operation = SynchronizationOperation.CreateDirectory(
            DestinationPath,
            "Destination directory is missing.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.CreateDirectory,
            null,
            DestinationPath,
            "Destination directory is missing.");
    }

    [Fact]
    public void ReplaceRepresentsSourceAndDestination()
    {
        var operation = SynchronizationOperation.Replace(
            SourcePath,
            DestinationPath,
            "Source content changed.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.Replace,
            SourcePath,
            DestinationPath,
            "Source content changed.");
    }

    [Fact]
    public void MoveRepresentsCurrentAndNewDestinationPaths()
    {
        var operation = SynchronizationOperation.Move(
            SourcePath,
            DestinationPath,
            "Stable identifier indicates a rename.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.Move,
            SourcePath,
            DestinationPath,
            "Stable identifier indicates a rename.");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SkipRepresentsAnyObservedPaths(bool hasSource, bool hasDestination)
    {
        var sourcePath = hasSource ? SourcePath : null;
        var destinationPath = hasDestination ? DestinationPath : null;

        var operation = SynchronizationOperation.Skip(
            sourcePath,
            destinationPath,
            "No action is required.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.Skip,
            sourcePath,
            destinationPath,
            "No action is required.");
    }

    [Fact]
    public void ConflictRepresentsBothSides()
    {
        var operation = SynchronizationOperation.Conflict(
            SourcePath,
            DestinationPath,
            "Both sides changed.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.Conflict,
            SourcePath,
            DestinationPath,
            "Both sides changed.");
    }

    [Fact]
    public void DeleteCandidateDoesNotAuthorizeADeleteOperation()
    {
        var operation = SynchronizationOperation.DeleteCandidate(
            DestinationPath,
            "Destination entry has no source counterpart.");

        AssertOperation(
            operation,
            SynchronizationOperationKind.DeleteCandidate,
            null,
            DestinationPath,
            "Destination entry has no source counterpart.");
    }

    [Fact]
    public void SkipRejectsMissingPaths()
    {
        Assert.Throws<ArgumentException>(() =>
            SynchronizationOperation.Skip(null, null, "No paths."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" reason")]
    [InlineData("reason ")]
    public void OperationsRejectInvalidReasons(string? reason)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            SynchronizationOperation.Copy(SourcePath, DestinationPath, reason!));
    }

    [Fact]
    public void OperationsRejectMissingRequiredPaths()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SynchronizationOperation.Copy(null!, DestinationPath, "Reason."));
        Assert.Throws<ArgumentNullException>(() =>
            SynchronizationOperation.Copy(SourcePath, null!, "Reason."));
        Assert.Throws<ArgumentNullException>(() =>
            SynchronizationOperation.CreateDirectory(null!, "Reason."));
        Assert.Throws<ArgumentNullException>(() =>
            SynchronizationOperation.DeleteCandidate(null!, "Reason."));
    }

    private static void AssertOperation(
        SynchronizationOperation operation,
        SynchronizationOperationKind expectedKind,
        StoragePath? expectedSourcePath,
        StoragePath? expectedDestinationPath,
        string expectedReason)
    {
        Assert.Equal(expectedKind, operation.Kind);
        Assert.Same(expectedSourcePath, operation.SourcePath);
        Assert.Same(expectedDestinationPath, operation.DestinationPath);
        Assert.Equal(expectedReason, operation.Reason);
    }
}
