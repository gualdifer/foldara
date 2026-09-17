using Foldara.Core;
using Foldara.Storage;
using Foldara.Testing;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class OneWaySynchronizationPlannerTests
{
    [Fact]
    public void PlanCreatesMissingDirectoriesBeforeTheirFiles()
    {
        var source = Snapshot(
            Directory("documents"),
            File("documents/file.txt", size: 5));
        var destination = Snapshot();

        var plan = Plan(source, destination);

        Assert.Collection(
            plan.Operations,
            operation => AssertOperation(
                operation,
                SynchronizationOperationKind.CreateDirectory,
                null,
                "documents"),
            operation => AssertOperation(
                operation,
                SynchronizationOperationKind.Copy,
                "documents/file.txt",
                "documents/file.txt"));
    }

    [Fact]
    public void PlanSkipsMatchingDirectoriesAndFiles()
    {
        var timestamp = new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
        var source = Snapshot(
            Directory("documents"),
            File("documents/file.txt", size: 5, lastModifiedAt: timestamp));
        var destination = Snapshot(
            Directory("documents"),
            File("documents/file.txt", size: 5, lastModifiedAt: timestamp));

        var plan = Plan(source, destination);

        Assert.All(
            plan.Operations,
            operation => Assert.Equal(SynchronizationOperationKind.Skip, operation.Kind));
    }

    [Fact]
    public void PlanUsesMatchingHashesBeforeTimestamps()
    {
        var source = Snapshot(File(
            "file.txt",
            size: 5,
            lastModifiedAt: DateTimeOffset.UnixEpoch,
            hash: new StorageContentHash("SHA-256", "abc")));
        var destination = Snapshot(File(
            "file.txt",
            size: 5,
            lastModifiedAt: DateTimeOffset.UnixEpoch.AddHours(1),
            hash: new StorageContentHash("sha-256", "abc")));

        var operation = Assert.Single(Plan(source, destination).Operations);

        Assert.Equal(SynchronizationOperationKind.Skip, operation.Kind);
        Assert.Equal("File content hash matches.", operation.Reason);
    }

    [Theory]
    [InlineData(5, 6, "abc", "abc", 0, 0, "File sizes differ.")]
    [InlineData(5, 5, "abc", "def", 0, 0, "File content hashes differ.")]
    [InlineData(5, 5, null, null, 0, 1, "File last-modified timestamps differ.")]
    public void PlanReplacesModifiedFiles(
        long sourceSize,
        long destinationSize,
        string? sourceHash,
        string? destinationHash,
        int sourceHours,
        int destinationHours,
        string expectedReason)
    {
        var source = Snapshot(File(
            "file.txt",
            sourceSize,
            DateTimeOffset.UnixEpoch.AddHours(sourceHours),
            CreateHash(sourceHash)));
        var destination = Snapshot(File(
            "file.txt",
            destinationSize,
            DateTimeOffset.UnixEpoch.AddHours(destinationHours),
            CreateHash(destinationHash)));

        var operation = Assert.Single(Plan(source, destination).Operations);

        Assert.Equal(SynchronizationOperationKind.Replace, operation.Kind);
        Assert.Equal(expectedReason, operation.Reason);
    }

    [Fact]
    public void PlanReplacesWhenMetadataCannotProveEquality()
    {
        var source = Snapshot(File("file.txt", size: 5));
        var destination = Snapshot(File("file.txt", size: 5));

        var operation = Assert.Single(Plan(source, destination).Operations);

        Assert.Equal(SynchronizationOperationKind.Replace, operation.Kind);
        Assert.Equal(
            "File metadata is insufficient to prove that content matches.",
            operation.Reason);
    }

    [Theory]
    [InlineData(StorageEntryKind.File, StorageEntryKind.Directory)]
    [InlineData(StorageEntryKind.Directory, StorageEntryKind.File)]
    public void PlanReportsTypeConflicts(
        StorageEntryKind sourceKind,
        StorageEntryKind destinationKind)
    {
        var source = Snapshot(Entry("entry", sourceKind));
        var destination = Snapshot(Entry("entry", destinationKind));

        var operation = Assert.Single(Plan(source, destination).Operations);

        Assert.Equal(SynchronizationOperationKind.Conflict, operation.Kind);
    }

    [Fact]
    public void PlanRecordsDestinationOnlyEntriesAsNonExecutableDeleteCandidates()
    {
        var source = Snapshot();
        var destination = Snapshot(
            Directory("obsolete"),
            File("obsolete/file.txt", size: 1));

        var plan = Plan(source, destination);

        Assert.Collection(
            plan.Operations,
            operation => AssertOperation(
                operation,
                SynchronizationOperationKind.DeleteCandidate,
                null,
                "obsolete/file.txt"),
            operation => AssertOperation(
                operation,
                SynchronizationOperationKind.DeleteCandidate,
                null,
                "obsolete"));
    }

    [Fact]
    public void PlanReportsSourceCaseCollisionsForInsensitiveDestination()
    {
        var source = Snapshot(
            StoragePathCaseSensitivity.Sensitive,
            File("File.txt", size: 1),
            File("file.txt", size: 1));
        var destination = Snapshot(StoragePathCaseSensitivity.Insensitive);

        var plan = Plan(source, destination);

        Assert.Equal(2, plan.Operations.Count);
        Assert.All(
            plan.Operations,
            operation => Assert.Equal(SynchronizationOperationKind.Conflict, operation.Kind));
    }

    [Fact]
    public void PlanUsesDestinationCaseSemanticsForExistingEntries()
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        var source = Snapshot(File("File.txt", 1, timestamp));
        var destination = Snapshot(
            StoragePathCaseSensitivity.Insensitive,
            File("file.txt", 1, timestamp));

        var operation = Assert.Single(Plan(source, destination).Operations);

        AssertOperation(
            operation,
            SynchronizationOperationKind.Skip,
            "File.txt",
            "file.txt");
    }

    [Fact]
    public void PlanIsIndependentOfSnapshotInputOrder()
    {
        var firstSource = Snapshot(File("b.txt", 1), File("a.txt", 1));
        var secondSource = Snapshot(File("a.txt", 1), File("b.txt", 1));
        var destination = Snapshot();
        var planner = new OneWaySynchronizationPlanner();

        var firstPlan = planner.Plan(
            firstSource,
            destination,
            TestContext.Current.CancellationToken);
        var secondPlan = planner.Plan(
            secondSource,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(firstPlan, secondPlan, SynchronizationPlanComparer.Instance);
    }

    [Fact]
    public async Task PlanHonorsPreCancelledToken()
    {
        var planner = new OneWaySynchronizationPlanner();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.Throws<OperationCanceledException>(() =>
            planner.Plan(Snapshot(), Snapshot(), cancellation.Token));
    }

    private static SynchronizationPlan Plan(
        StorageSnapshot source,
        StorageSnapshot destination) =>
        new OneWaySynchronizationPlanner().Plan(
            source,
            destination,
            TestContext.Current.CancellationToken);

    private static StorageSnapshot Snapshot(params StorageEntry[] entries) =>
        Snapshot(StoragePathCaseSensitivity.Sensitive, entries);

    private static StorageSnapshot Snapshot(
        StoragePathCaseSensitivity caseSensitivity,
        params StorageEntry[] entries) =>
        new(caseSensitivity, entries);

    private static StorageEntry Entry(string path, StorageEntryKind kind) =>
        kind == StorageEntryKind.File ? File(path, size: 0) : Directory(path);

    private static StorageEntry File(
        string path,
        long? size = null,
        DateTimeOffset? lastModifiedAt = null,
        StorageContentHash? hash = null) =>
        new(
            StoragePath.Parse(path),
            StorageEntryKind.File,
            new StorageEntryMetadata(
                size: size,
                lastModifiedAt: lastModifiedAt,
                contentHash: hash));

    private static StorageEntry Directory(string path) =>
        new(StoragePath.Parse(path), StorageEntryKind.Directory);

    private static StorageContentHash? CreateHash(string? value) =>
        value is null ? null : new StorageContentHash("SHA-256", value);

    private static void AssertOperation(
        SynchronizationOperation operation,
        SynchronizationOperationKind kind,
        string? sourcePath,
        string? destinationPath)
    {
        Assert.Equal(kind, operation.Kind);
        Assert.Equal(sourcePath, operation.SourcePath?.Value);
        Assert.Equal(destinationPath, operation.DestinationPath?.Value);
    }
}
