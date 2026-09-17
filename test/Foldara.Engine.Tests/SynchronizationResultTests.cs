using Foldara.Core;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class SynchronizationResultTests
{
    [Fact]
    public void RunResultCopiesOperationsAndPreservesCorrelation()
    {
        var runId = Guid.NewGuid();
        var operationResult = SuccessfulOperation(runId, Guid.NewGuid());
        var source = new List<SynchronizationOperationResult> { operationResult };

        var result = new SynchronizationRunResult(
            runId,
            TimeSpan.FromSeconds(1),
            SynchronizationRunOutcome.Succeeded,
            source);
        source.Clear();

        Assert.Equal(runId, result.RunId);
        Assert.Same(operationResult, Assert.Single(result.Operations));
        Assert.Equal(TimeSpan.FromSeconds(1), result.Duration);
    }

    [Fact]
    public void OperationResultRequiresErrorDetailsForFailure()
    {
        Assert.Throws<ArgumentException>(() => new SynchronizationOperationResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateSkipOperation(),
            TimeSpan.Zero,
            SynchronizationOperationOutcome.Failed));
    }

    [Fact]
    public void RunResultRejectsDuplicateOperationIdentifiers()
    {
        var runId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new SynchronizationRunResult(
            runId,
            TimeSpan.Zero,
            SynchronizationRunOutcome.Succeeded,
            [
                SuccessfulOperation(runId, operationId),
                SuccessfulOperation(runId, operationId),
            ]));
    }

    private static SynchronizationOperationResult SuccessfulOperation(
        Guid runId,
        Guid operationId) =>
        new(
            runId,
            operationId,
            CreateSkipOperation(),
            TimeSpan.Zero,
            SynchronizationOperationOutcome.Skipped);

    private static SynchronizationOperation CreateSkipOperation()
    {
        var path = StoragePath.Parse("file.txt");
        return SynchronizationOperation.Skip(path, path, "File is unchanged.");
    }
}
