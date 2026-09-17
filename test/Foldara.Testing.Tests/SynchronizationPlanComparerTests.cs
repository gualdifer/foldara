using Foldara.Core;
using Xunit;

namespace Foldara.Testing.Tests;

public sealed class SynchronizationPlanComparerTests
{
    [Fact]
    public void EqualsAcceptsStructurallyEqualPlans()
    {
        var expected = CreatePlan(copyFirst: true);
        var actual = CreatePlan(copyFirst: true);

        Assert.Equal(expected, actual, SynchronizationPlanComparer.Instance);
        Assert.Equal(
            SynchronizationPlanComparer.Instance.GetHashCode(expected),
            SynchronizationPlanComparer.Instance.GetHashCode(actual));
    }

    [Fact]
    public void EqualsDetectsDifferentOperationOrder()
    {
        var expected = CreatePlan(copyFirst: true);
        var actual = CreatePlan(copyFirst: false);

        Assert.NotEqual(expected, actual, SynchronizationPlanComparer.Instance);
    }

    [Fact]
    public void EqualsDetectsDifferentReasons()
    {
        var path = StoragePath.Parse("file.txt");
        var expected = new SynchronizationPlan(
        [
            SynchronizationOperation.Copy(path, path, "Source is new."),
        ]);
        var actual = new SynchronizationPlan(
        [
            SynchronizationOperation.Copy(path, path, "Source changed."),
        ]);

        Assert.NotEqual(expected, actual, SynchronizationPlanComparer.Instance);
    }

    private static SynchronizationPlan CreatePlan(bool copyFirst)
    {
        var directory = SynchronizationOperation.CreateDirectory(
            StoragePath.Parse("documents"),
            "Directory is missing.");
        var copy = SynchronizationOperation.Copy(
            StoragePath.Parse("documents/file.txt"),
            StoragePath.Parse("documents/file.txt"),
            "File is missing.");

        return new SynchronizationPlan(copyFirst ? [copy, directory] : [directory, copy]);
    }
}
