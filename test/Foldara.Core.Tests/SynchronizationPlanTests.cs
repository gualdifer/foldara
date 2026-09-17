using System.Text.Json;
using Xunit;

namespace Foldara.Core.Tests;

public sealed class SynchronizationPlanTests
{
    [Fact]
    public void ConstructorDefensivelyCopiesOperationsInOrder()
    {
        var first = SynchronizationOperation.CreateDirectory(
            StoragePath.Parse("Documents"),
            "Directory is missing.");
        var second = SynchronizationOperation.Copy(
            StoragePath.Parse("Documents/file.txt"),
            StoragePath.Parse("Documents/file.txt"),
            "File is missing.");
        var input = new List<SynchronizationOperation> { first, second };

        var plan = new SynchronizationPlan(input);
        input.Clear();

        Assert.Equal(2, plan.Operations.Count);
        Assert.Same(first, plan.Operations[0]);
        Assert.Same(second, plan.Operations[1]);
    }

    [Fact]
    public void ConstructorAllowsAnEmptyPlan()
    {
        var plan = new SynchronizationPlan(Array.Empty<SynchronizationOperation>());

        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void ConstructorRejectsNullOperationsCollection()
    {
        Assert.Throws<ArgumentNullException>(() => new SynchronizationPlan(null!));
    }

    [Fact]
    public void ConstructorRejectsNullOperation()
    {
        var operations = new SynchronizationOperation[] { null! };

        Assert.Throws<ArgumentException>(() => new SynchronizationPlan(operations));
    }

    [Fact]
    public void DiagnosticJsonUsesReadableKindsAndNormalizedPathStrings()
    {
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.Copy(
                StoragePath.Parse("source/file.txt"),
                StoragePath.Parse("destination/file.txt"),
                "Source is new."),
            SynchronizationOperation.DeleteCandidate(
                StoragePath.Parse("obsolete.txt"),
                "No source counterpart."),
        ]);

        var json = plan.ToDiagnosticJson();
        using var document = JsonDocument.Parse(json);
        var operations = document.RootElement.GetProperty("Operations");

        Assert.Equal(2, operations.GetArrayLength());
        Assert.Equal("Copy", operations[0].GetProperty("Kind").GetString());
        Assert.Equal("source/file.txt", operations[0].GetProperty("SourcePath").GetString());
        Assert.Equal(
            "destination/file.txt",
            operations[0].GetProperty("DestinationPath").GetString());
        Assert.Equal("Source is new.", operations[0].GetProperty("Reason").GetString());
        Assert.Equal("DeleteCandidate", operations[1].GetProperty("Kind").GetString());
        Assert.False(operations[1].TryGetProperty("SourcePath", out _));
        Assert.Equal("obsolete.txt", operations[1].GetProperty("DestinationPath").GetString());
    }

    [Fact]
    public void DiagnosticJsonCanBeIndented()
    {
        var plan = new SynchronizationPlan(Array.Empty<SynchronizationOperation>());

        var json = plan.ToDiagnosticJson(indented: true);

        Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
    }
}
