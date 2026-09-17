using Foldara.Core;
using Foldara.Storage;
using Foldara.Storage.Local;
using Foldara.Testing;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class LocalSynchronizationEndToEndTests
{
    [Fact]
    public async Task CompletedPlanCanBeRepeatedAcrossNestedEmptyUpdatedAndRenamedFiles()
    {
        var emptyPath = StoragePath.Parse("empty.bin");
        var nestedFilePath = StoragePath.Parse("nested/deeper/file.txt");
        var newNamePath = StoragePath.Parse("new-name.txt");
        var oldNamePath = StoragePath.Parse("old-name.txt");
        var updatedPath = StoragePath.Parse("updated.txt");
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteFile(emptyPath, Array.Empty<byte>())
            .WriteTextFile(nestedFilePath, "nested contents")
            .WriteTextFile(newNamePath, "renamed contents")
            .WriteTextFile(updatedPath, "updated contents");
        using var destinationDirectory = TemporaryDirectory.Create()
            .WriteTextFile(oldNamePath, "renamed contents")
            .WriteTextFile(updatedPath, "old");
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var plan = await CreatePlanAsync(source, destination);

        Assert.Contains(
            plan.Operations,
            operation => operation.Kind == SynchronizationOperationKind.CreateDirectory &&
                operation.DestinationPath == StoragePath.Parse("nested/deeper"));
        Assert.Contains(
            plan.Operations,
            operation => operation.Kind == SynchronizationOperationKind.Copy &&
                operation.SourcePath == emptyPath);
        Assert.Contains(
            plan.Operations,
            operation => operation.Kind == SynchronizationOperationKind.Replace &&
                operation.SourcePath == updatedPath);
        Assert.Contains(
            plan.Operations,
            operation => operation.Kind == SynchronizationOperationKind.Copy &&
                operation.SourcePath == newNamePath);
        Assert.Contains(
            plan.Operations,
            operation => operation.Kind == SynchronizationOperationKind.DeleteCandidate &&
                operation.DestinationPath == oldNamePath);

        var executor = new SynchronizationPlanExecutor();
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstResult = await executor.ExecuteAsync(
            plan,
            source,
            destination,
            cancellationToken);
        var secondResult = await executor.ExecuteAsync(
            plan,
            source,
            destination,
            cancellationToken);

        Assert.Equal(SynchronizationRunOutcome.Succeeded, firstResult.Outcome);
        Assert.Equal(SynchronizationRunOutcome.Succeeded, secondResult.Outcome);
        Assert.Equal(plan.Operations.Count, firstResult.Operations.Count);
        Assert.Equal(plan.Operations.Count, secondResult.Operations.Count);
        Assert.Empty(File.ReadAllBytes(destinationDirectory.GetFullPath(emptyPath)));
        Assert.Equal(
            "nested contents",
            File.ReadAllText(destinationDirectory.GetFullPath(nestedFilePath)));
        Assert.Equal(
            "renamed contents",
            File.ReadAllText(destinationDirectory.GetFullPath(newNamePath)));
        Assert.Equal(
            "updated contents",
            File.ReadAllText(destinationDirectory.GetFullPath(updatedPath)));
        Assert.Equal(
            "renamed contents",
            File.ReadAllText(destinationDirectory.GetFullPath(oldNamePath)));
        Assert.Equal(
            [
                "empty.bin",
                "nested/",
                "nested/deeper/",
                "nested/deeper/file.txt",
                "new-name.txt",
                "old-name.txt",
                "updated.txt",
            ],
            destinationDirectory.GetTreeSnapshot());
    }

    [Fact]
    public async Task CancelledExecutionDoesNotMutateDestination()
    {
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("a.txt"), "a")
            .WriteTextFile(StoragePath.Parse("nested/b.txt"), "b");
        using var destinationDirectory = TemporaryDirectory.Create();
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var plan = await CreatePlanAsync(source, destination);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await new SynchronizationPlanExecutor().ExecuteAsync(
            plan,
            source,
            destination,
            cancellation.Token);

        Assert.Equal(SynchronizationRunOutcome.Cancelled, result.Outcome);
        Assert.Equal(SynchronizationErrorCode.Cancelled, result.Error?.Code);
        Assert.All(
            result.Operations,
            operation => Assert.Equal(
                SynchronizationOperationOutcome.NotRun,
                operation.Outcome));
        Assert.Empty(destinationDirectory.GetTreeSnapshot());
    }

    [Fact]
    public async Task SourceDisappearingAfterPlanningStopsLaterWorkAndPreservesCompletedWork()
    {
        var completedPath = StoragePath.Parse("a-completed.txt");
        var disappearedPath = StoragePath.Parse("b-disappeared.txt");
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteTextFile(completedPath, "completed")
            .WriteTextFile(disappearedPath, "disappeared");
        using var destinationDirectory = TemporaryDirectory.Create();
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var plan = await CreatePlanAsync(source, destination);
        File.Delete(sourceDirectory.GetFullPath(disappearedPath));

        var result = await new SynchronizationPlanExecutor().ExecuteAsync(
            plan,
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(SynchronizationRunOutcome.Failed, result.Outcome);
        Assert.Equal(SynchronizationErrorCode.SourceChanged, result.Error?.Code);
        Assert.True(result.Error?.IsRetryable);
        Assert.Collection(
            result.Operations,
            operation =>
            {
                Assert.Equal(completedPath, operation.Operation.SourcePath);
                Assert.Equal(SynchronizationOperationOutcome.Succeeded, operation.Outcome);
            },
            operation =>
            {
                Assert.Equal(disappearedPath, operation.Operation.SourcePath);
                Assert.Equal(SynchronizationOperationOutcome.Failed, operation.Outcome);
            });
        Assert.Equal(
            "completed",
            File.ReadAllText(destinationDirectory.GetFullPath(completedPath)));
        Assert.False(File.Exists(destinationDirectory.GetFullPath(disappearedPath)));
        Assert.Equal(["a-completed.txt"], destinationDirectory.GetTreeSnapshot());
    }

    private static async ValueTask<SynchronizationPlan> CreatePlanAsync(
        IStorageProvider source,
        IStorageProvider destination)
    {
        var scanner = new StorageScanner();
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceSnapshot = await scanner.ScanAsync(source, cancellationToken);
        var destinationSnapshot = await scanner.ScanAsync(destination, cancellationToken);

        return new OneWaySynchronizationPlanner().Plan(
            sourceSnapshot,
            destinationSnapshot,
            cancellationToken);
    }
}
