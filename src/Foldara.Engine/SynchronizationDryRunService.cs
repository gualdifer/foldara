using Foldara.Core;
using Foldara.Storage;

namespace Foldara.Engine;

/// <summary>
/// Creates synchronization plans without invoking provider mutation operations.
/// </summary>
public sealed class SynchronizationDryRunService
{
    private readonly OneWaySynchronizationPlanner _planner;
    private readonly StorageScanner _scanner;

    public SynchronizationDryRunService()
        : this(new StorageScanner(), new OneWaySynchronizationPlanner())
    {
    }

    public SynchronizationDryRunService(
        StorageScanner scanner,
        OneWaySynchronizationPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(planner);

        _scanner = scanner;
        _planner = planner;
    }

    public async ValueTask<SynchronizationPlan> CreatePlanAsync(
        IStorageProvider source,
        IStorageProvider destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceSnapshot = await _scanner
            .ScanAsync(source, cancellationToken)
            .ConfigureAwait(false);
        var destinationSnapshot = await _scanner
            .ScanAsync(destination, cancellationToken)
            .ConfigureAwait(false);

        return _planner.Plan(sourceSnapshot, destinationSnapshot, cancellationToken);
    }
}
