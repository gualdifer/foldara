using Foldara.Core;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class SynchronizationPlanTextFormatterTests
{
    [Fact]
    public void FormatIncludesSummaryPathsAndReasonsForEveryOperation()
    {
        var source = StoragePath.Parse("source/file.txt");
        var destination = StoragePath.Parse("destination/file.txt");
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.Copy(source, destination, "Copy reason."),
            SynchronizationOperation.CreateDirectory(destination.Parent!, "Directory reason."),
            SynchronizationOperation.Replace(source, destination, "Replace reason."),
            SynchronizationOperation.Move(source, destination, "Move reason."),
            SynchronizationOperation.Skip(source, destination, "Skip reason."),
            SynchronizationOperation.Conflict(source, destination, "Conflict reason."),
            SynchronizationOperation.DeleteCandidate(destination, "Delete candidate reason."),
        ]);
        var formatter = new SynchronizationPlanTextFormatter();

        var output = formatter.Format(plan);

        Assert.Contains("DRY RUN - no changes will be applied", output, StringComparison.Ordinal);
        Assert.Contains("Total operations: 7", output, StringComparison.Ordinal);
        Assert.Contains("Copy: 1", output, StringComparison.Ordinal);
        Assert.Contains("CreateDirectory: 1", output, StringComparison.Ordinal);
        Assert.Contains("Replace: 1", output, StringComparison.Ordinal);
        Assert.Contains("Move: 1", output, StringComparison.Ordinal);
        Assert.Contains("Skip: 1", output, StringComparison.Ordinal);
        Assert.Contains("Conflict: 1", output, StringComparison.Ordinal);
        Assert.Contains("DeleteCandidate: 1", output, StringComparison.Ordinal);

        for (var index = 0; index < plan.Operations.Count; index++)
        {
            Assert.Contains(
                $"reason=\"{plan.Operations[index].Reason}\"",
                output,
                StringComparison.Ordinal);
        }

        Assert.Contains("source=\"source/file.txt\"", output, StringComparison.Ordinal);
        Assert.Contains("destination=\"destination/file.txt\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEscapesUntrustedDiagnosticText()
    {
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.Skip(
                StoragePath.Parse("line\nbreak.txt"),
                null,
                "reason\r\nnext"),
        ]);
        var formatter = new SynchronizationPlanTextFormatter();

        var output = formatter.Format(plan);

        Assert.DoesNotContain("line\nbreak", output, StringComparison.Ordinal);
        Assert.DoesNotContain("reason\r\nnext", output, StringComparison.Ordinal);
        Assert.Contains("line\\nbreak.txt", output, StringComparison.Ordinal);
        Assert.Contains("reason\\r\\nnext", output, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRepresentsAnEmptyPlan()
    {
        var plan = new SynchronizationPlan(Array.Empty<SynchronizationOperation>());
        var formatter = new SynchronizationPlanTextFormatter();

        var output = formatter.Format(plan);

        Assert.Contains("Total operations: 0", output, StringComparison.Ordinal);
        Assert.EndsWith("Operations:\n", output, StringComparison.Ordinal);
    }
}
