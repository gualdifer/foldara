using Foldara.Core;

namespace Foldara.Testing;

/// <summary>
/// Compares synchronization plans structurally in their deterministic operation order.
/// </summary>
public sealed class SynchronizationPlanComparer : IEqualityComparer<SynchronizationPlan>
{
    private SynchronizationPlanComparer()
    {
    }

    public static SynchronizationPlanComparer Instance { get; } = new();

    public bool Equals(SynchronizationPlan? x, SynchronizationPlan? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Operations.Count != y.Operations.Count)
        {
            return false;
        }

        for (var index = 0; index < x.Operations.Count; index++)
        {
            if (!OperationsEqual(x.Operations[index], y.Operations[index]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(SynchronizationPlan obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var hash = new HashCode();

        foreach (var operation in obj.Operations)
        {
            hash.Add(operation.Kind);
            hash.Add(operation.SourcePath);
            hash.Add(operation.DestinationPath);
            hash.Add(operation.Reason, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static bool OperationsEqual(
        SynchronizationOperation x,
        SynchronizationOperation y) =>
        x.Kind == y.Kind &&
        x.SourcePath == y.SourcePath &&
        x.DestinationPath == y.DestinationPath &&
        string.Equals(x.Reason, y.Reason, StringComparison.Ordinal);
}
