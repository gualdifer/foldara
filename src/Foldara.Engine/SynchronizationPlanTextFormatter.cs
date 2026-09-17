using System.Globalization;
using System.Text;
using System.Text.Json;
using Foldara.Core;

namespace Foldara.Engine;

/// <summary>
/// Formats a synchronization plan for human-readable dry-run output.
/// </summary>
public sealed class SynchronizationPlanTextFormatter
{
    public string Format(SynchronizationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var output = new StringBuilder();
        output.Append("DRY RUN - no changes will be applied\n");
        output.Append("Total operations: ");
        output.Append(plan.Operations.Count.ToString(CultureInfo.InvariantCulture));
        output.Append('\n');

        foreach (var kind in Enum.GetValues<SynchronizationOperationKind>())
        {
            output.Append(GetDisplayName(kind));
            output.Append(": ");
            output.Append(plan.Operations
                .Count(operation => operation.Kind == kind)
                .ToString(CultureInfo.InvariantCulture));
            output.Append('\n');
        }

        output.Append('\n');
        output.Append("Operations:\n");

        for (var index = 0; index < plan.Operations.Count; index++)
        {
            AppendOperation(output, index, plan.Operations[index]);
        }

        return output.ToString();
    }

    private static void AppendOperation(
        StringBuilder output,
        int index,
        SynchronizationOperation operation)
    {
        output.Append((index + 1).ToString("D4", CultureInfo.InvariantCulture));
        output.Append(' ');
        output.Append(GetDisplayName(operation.Kind).ToUpperInvariant());

        if (operation.SourcePath is not null)
        {
            output.Append(" source=");
            output.Append(Quote(operation.SourcePath.Value));
        }

        if (operation.DestinationPath is not null)
        {
            output.Append(" destination=");
            output.Append(Quote(operation.DestinationPath.Value));
        }

        output.Append(" reason=");
        output.Append(Quote(operation.Reason));
        output.Append('\n');
    }

    private static string GetDisplayName(SynchronizationOperationKind kind) =>
        kind switch
        {
            SynchronizationOperationKind.Copy => "Copy",
            SynchronizationOperationKind.CreateDirectory => "CreateDirectory",
            SynchronizationOperationKind.Replace => "Replace",
            SynchronizationOperationKind.Move => "Move",
            SynchronizationOperationKind.Skip => "Skip",
            SynchronizationOperationKind.Conflict => "Conflict",
            SynchronizationOperationKind.DeleteCandidate => "DeleteCandidate",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown operation kind."),
        };

    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
