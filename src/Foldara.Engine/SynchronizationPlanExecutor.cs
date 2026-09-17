using System.Diagnostics;
using Foldara.Core;
using Foldara.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foldara.Engine;

/// <summary>
/// Executes validated synchronization plans sequentially through provider contracts.
/// </summary>
public sealed class SynchronizationPlanExecutor
{
    private const int TransferBufferSize = 81920;
    private readonly ILogger<SynchronizationPlanExecutor> _logger;

    public SynchronizationPlanExecutor(ILogger<SynchronizationPlanExecutor>? logger = null)
    {
        _logger = logger ?? NullLogger<SynchronizationPlanExecutor>.Instance;
    }

    public async ValueTask<SynchronizationRunResult> ExecuteAsync(
        SynchronizationPlan plan,
        IStorageProvider source,
        IStorageProvider destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var runId = Guid.NewGuid();
        var runStarted = Stopwatch.GetTimestamp();
        var trackedOperations = plan.Operations
            .Select(operation => new TrackedOperation(operation, Guid.NewGuid()))
            .ToArray();
        var results = new List<SynchronizationOperationResult>(trackedOperations.Length);

        _logger.LogInformation(
            "Synchronization run {RunId} started with {OperationCount} operations.",
            runId,
            trackedOperations.Length);

        if (cancellationToken.IsCancellationRequested)
        {
            var error = CancelledError();
            AddRemainingResults(
                results,
                trackedOperations,
                runId,
                startIndex: 0);
            return CompleteRun(
                runId,
                runStarted,
                SynchronizationRunOutcome.Cancelled,
                results,
                error);
        }

        try
        {
            ValidatePlan(plan, destination);
        }
        catch (SynchronizationExecutionException exception)
        {
            var failedIndex = Array.FindIndex(
                trackedOperations,
                tracked => ReferenceEquals(tracked.Operation, exception.Operation));
            var error = CreateError(exception);

            AddRemainingResults(
                results,
                trackedOperations,
                runId,
                startIndex: 0,
                failedIndex: failedIndex,
                failure: error);

            return CompleteRun(
                runId,
                runStarted,
                SynchronizationRunOutcome.Failed,
                results,
                error);
        }

        for (var index = 0; index < trackedOperations.Length; index++)
        {
            var tracked = trackedOperations[index];
            var operationStarted = Stopwatch.GetTimestamp();

            _logger.LogInformation(
                "Synchronization operation {OperationId} in run {RunId} started as {OperationKind}.",
                tracked.OperationId,
                runId,
                tracked.Operation.Kind);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteOperationAsync(
                    tracked.Operation,
                    source,
                    destination,
                    cancellationToken).ConfigureAwait(false);

                var outcome = tracked.Operation.Kind is SynchronizationOperationKind.Skip
                    or SynchronizationOperationKind.DeleteCandidate
                    ? SynchronizationOperationOutcome.Skipped
                    : SynchronizationOperationOutcome.Succeeded;
                var operationResult = new SynchronizationOperationResult(
                    runId,
                    tracked.OperationId,
                    tracked.Operation,
                    Stopwatch.GetElapsedTime(operationStarted),
                    outcome);
                results.Add(operationResult);
                LogOperationCompleted(operationResult);
            }
            catch (Exception exception)
            {
                var isCancelled = exception is OperationCanceledException &&
                    cancellationToken.IsCancellationRequested;
                var outcome = isCancelled
                    ? SynchronizationOperationOutcome.Cancelled
                    : SynchronizationOperationOutcome.Failed;
                var error = isCancelled ? CancelledError() : CreateError(exception);
                var operationResult = new SynchronizationOperationResult(
                    runId,
                    tracked.OperationId,
                    tracked.Operation,
                    Stopwatch.GetElapsedTime(operationStarted),
                    outcome,
                    error);
                results.Add(operationResult);
                LogOperationCompleted(operationResult);
                AddRemainingResults(
                    results,
                    trackedOperations,
                    runId,
                    index + 1);

                return CompleteRun(
                    runId,
                    runStarted,
                    isCancelled
                        ? SynchronizationRunOutcome.Cancelled
                        : SynchronizationRunOutcome.Failed,
                    results,
                    error);
            }
        }

        return CompleteRun(
            runId,
            runStarted,
            SynchronizationRunOutcome.Succeeded,
            results,
            error: null);
    }

    private static async ValueTask ExecuteOperationAsync(
        SynchronizationOperation operation,
        IStorageProvider source,
        IStorageProvider destination,
        CancellationToken cancellationToken)
    {
        switch (operation.Kind)
        {
            case SynchronizationOperationKind.Copy:
                await ExecuteCopyAsync(
                    operation,
                    source,
                    destination,
                    cancellationToken).ConfigureAwait(false);
                break;

            case SynchronizationOperationKind.CreateDirectory:
                await destination.CreateDirectoryAsync(
                    operation.DestinationPath!,
                    cancellationToken).ConfigureAwait(false);
                break;

            case SynchronizationOperationKind.Replace:
                await TransferAsync(
                    operation,
                    source,
                    destination,
                    StorageWriteMode.CreateOrReplace,
                    cancellationToken).ConfigureAwait(false);
                break;

            case SynchronizationOperationKind.Move:
                await ExecuteMoveAsync(
                    operation,
                    destination,
                    cancellationToken).ConfigureAwait(false);
                break;

            case SynchronizationOperationKind.Skip:
            case SynchronizationOperationKind.DeleteCandidate:
                break;

            case SynchronizationOperationKind.Conflict:
                throw new UnreachableException("Conflicts are rejected during plan validation.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation.Kind,
                    "Unknown synchronization operation kind.");
        }
    }

    private static void ValidatePlan(
        SynchronizationPlan plan,
        IStorageProvider destination)
    {
        foreach (var operation in plan.Operations)
        {
            if (operation.Kind == SynchronizationOperationKind.Conflict)
            {
                throw new SynchronizationExecutionException(
                    operation,
                    SynchronizationErrorCode.PlanConflict,
                    "A synchronization plan containing conflicts cannot be executed.");
            }

            if (operation.Kind == SynchronizationOperationKind.Move &&
                !destination.Capabilities.SupportsMove)
            {
                throw new SynchronizationExecutionException(
                    operation,
                    SynchronizationErrorCode.UnsupportedMove,
                    "The destination provider does not support move operations.");
            }
        }
    }

    private static async ValueTask ExecuteCopyAsync(
        SynchronizationOperation operation,
        IStorageProvider source,
        IStorageProvider destination,
        CancellationToken cancellationToken)
    {
        if (await DestinationContainsSourceContentAsync(
            operation,
            source,
            destination,
            cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var existingDestination = await destination.GetEntryAsync(
            operation.DestinationPath!,
            cancellationToken).ConfigureAwait(false);

        if (existingDestination is not null)
        {
            throw new SynchronizationExecutionException(
                operation,
                SynchronizationErrorCode.DestinationConflict,
                "The copy destination already exists with different content.");
        }

        try
        {
            await TransferAsync(
                operation,
                source,
                destination,
                StorageWriteMode.CreateNew,
                cancellationToken).ConfigureAwait(false);
        }
        catch (StorageProviderException exception) when (
            exception.Error == StorageProviderError.AlreadyExists)
        {
            if (await DestinationContainsSourceContentAsync(
                operation,
                source,
                destination,
                cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            throw new SynchronizationExecutionException(
                operation,
                SynchronizationErrorCode.DestinationConflict,
                "The copy destination appeared during execution with different content.",
                exception);
        }
    }

    private static async ValueTask TransferAsync(
        SynchronizationOperation operation,
        IStorageProvider source,
        IStorageProvider destination,
        StorageWriteMode writeMode,
        CancellationToken cancellationToken)
    {
        var sourceBeforeTransfer = await GetSourceFileAsync(
            operation,
            source,
            cancellationToken).ConfigureAwait(false);

        await using var sourceStream = await source.OpenReadAsync(
            operation.SourcePath!,
            cancellationToken).ConfigureAwait(false);
        await using var writeSession = await destination.BeginWriteAsync(
            operation.DestinationPath!,
            writeMode,
            cancellationToken).ConfigureAwait(false);

        var transferredBytes = await CopyToAsync(
            sourceStream,
            writeSession.Content,
            cancellationToken).ConfigureAwait(false);

        var sourceAfterTransfer = await GetSourceFileAsync(
            operation,
            source,
            cancellationToken).ConfigureAwait(false);

        if (!IsConsistentTransfer(
            sourceBeforeTransfer.Metadata,
            sourceAfterTransfer.Metadata,
            transferredBytes))
        {
            throw SourceChanged(operation);
        }

        await writeSession.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<StorageEntry> GetSourceFileAsync(
        SynchronizationOperation operation,
        IStorageProvider source,
        CancellationToken cancellationToken)
    {
        var entry = await source.GetEntryAsync(
            operation.SourcePath!,
            cancellationToken).ConfigureAwait(false);

        if (entry is null || entry.Kind != StorageEntryKind.File)
        {
            throw SourceChanged(operation);
        }

        return entry;
    }

    private static async ValueTask<long> CopyToAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[TransferBufferSize];
        long transferredBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(
                buffer,
                cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                return transferredBytes;
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken).ConfigureAwait(false);
            transferredBytes = checked(transferredBytes + bytesRead);
        }
    }

    private static bool IsConsistentTransfer(
        StorageEntryMetadata before,
        StorageEntryMetadata after,
        long transferredBytes) =>
        before.Size == after.Size &&
        (before.Size is null || before.Size == transferredBytes) &&
        before.CreatedAt == after.CreatedAt &&
        before.LastModifiedAt == after.LastModifiedAt &&
        string.Equals(before.ProviderItemId, after.ProviderItemId, StringComparison.Ordinal) &&
        ContentHashesEqual(before.ContentHash, after.ContentHash) &&
        string.Equals(before.Revision, after.Revision, StringComparison.Ordinal);

    private static bool ContentHashesEqual(
        StorageContentHash? before,
        StorageContentHash? after)
    {
        if (before is null || after is null)
        {
            return before is null && after is null;
        }

        return string.Equals(before.Algorithm, after.Algorithm, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(before.Value, after.Value, StringComparison.Ordinal);
    }

    private static SynchronizationExecutionException SourceChanged(
        SynchronizationOperation operation) =>
        new(
            operation,
            SynchronizationErrorCode.SourceChanged,
            "The source file changed while it was being transferred; retry with a new plan.",
            isRetryable: true);

    private static async ValueTask ExecuteMoveAsync(
        SynchronizationOperation operation,
        IStorageProvider destination,
        CancellationToken cancellationToken)
    {
        var sourceEntry = await destination.GetEntryAsync(
            operation.SourcePath!,
            cancellationToken).ConfigureAwait(false);

        if (sourceEntry is null)
        {
            var destinationEntry = await destination.GetEntryAsync(
                operation.DestinationPath!,
                cancellationToken).ConfigureAwait(false);

            if (destinationEntry is not null)
            {
                return;
            }

            throw new SynchronizationExecutionException(
                operation,
                SynchronizationErrorCode.MoveSourceMissing,
                "Neither the move source nor destination exists.");
        }

        try
        {
            await destination.MoveAsync(
                operation.SourcePath!,
                operation.DestinationPath!,
                StorageWriteMode.CreateNew,
                cancellationToken).ConfigureAwait(false);
        }
        catch (StorageProviderException exception) when (
            exception.Error == StorageProviderError.NotFound)
        {
            var destinationEntry = await destination.GetEntryAsync(
                operation.DestinationPath!,
                cancellationToken).ConfigureAwait(false);

            if (destinationEntry is not null)
            {
                return;
            }

            throw;
        }
    }

    private static async ValueTask<bool> DestinationContainsSourceContentAsync(
        SynchronizationOperation operation,
        IStorageProvider source,
        IStorageProvider destination,
        CancellationToken cancellationToken)
    {
        var sourceEntry = await source.GetEntryAsync(
            operation.SourcePath!,
            cancellationToken).ConfigureAwait(false);
        var destinationEntry = await destination.GetEntryAsync(
            operation.DestinationPath!,
            cancellationToken).ConfigureAwait(false);

        if (sourceEntry is null ||
            destinationEntry is null ||
            sourceEntry.Kind != StorageEntryKind.File ||
            destinationEntry.Kind != StorageEntryKind.File)
        {
            return false;
        }

        if (sourceEntry.Metadata.Size is not null &&
            destinationEntry.Metadata.Size is not null &&
            sourceEntry.Metadata.Size != destinationEntry.Metadata.Size)
        {
            return false;
        }

        if (CanCompareHashes(sourceEntry.Metadata.ContentHash, destinationEntry.Metadata.ContentHash))
        {
            return string.Equals(
                sourceEntry.Metadata.ContentHash!.Value,
                destinationEntry.Metadata.ContentHash!.Value,
                StringComparison.Ordinal);
        }

        await using var sourceStream = await source.OpenReadAsync(
            operation.SourcePath!,
            cancellationToken).ConfigureAwait(false);
        await using var destinationStream = await destination.OpenReadAsync(
            operation.DestinationPath!,
            cancellationToken).ConfigureAwait(false);

        return await StreamsEqualAsync(
            sourceStream,
            destinationStream,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool CanCompareHashes(
        StorageContentHash? source,
        StorageContentHash? destination) =>
        source is not null &&
        destination is not null &&
        string.Equals(source.Algorithm, destination.Algorithm, StringComparison.OrdinalIgnoreCase);

    private static async ValueTask<bool> StreamsEqualAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var sourceBuffer = new byte[TransferBufferSize];
        var destinationBuffer = new byte[TransferBufferSize];

        while (true)
        {
            var sourceBytesRead = await source.ReadAtLeastAsync(
                sourceBuffer,
                sourceBuffer.Length,
                throwOnEndOfStream: false,
                cancellationToken).ConfigureAwait(false);
            var destinationBytesRead = await destination.ReadAtLeastAsync(
                destinationBuffer,
                destinationBuffer.Length,
                throwOnEndOfStream: false,
                cancellationToken).ConfigureAwait(false);

            if (sourceBytesRead != destinationBytesRead)
            {
                return false;
            }

            if (sourceBytesRead == 0)
            {
                return true;
            }

            if (!sourceBuffer.AsSpan(0, sourceBytesRead)
                .SequenceEqual(destinationBuffer.AsSpan(0, destinationBytesRead)))
            {
                return false;
            }
        }
    }

    private void AddRemainingResults(
        ICollection<SynchronizationOperationResult> results,
        IReadOnlyList<TrackedOperation> trackedOperations,
        Guid runId,
        int startIndex,
        int failedIndex = -1,
        SynchronizationErrorDetails? failure = null)
    {
        for (var index = startIndex; index < trackedOperations.Count; index++)
        {
            var tracked = trackedOperations[index];
            var isExplicitFailure = index == failedIndex;
            var outcome = isExplicitFailure
                ? SynchronizationOperationOutcome.Failed
                : SynchronizationOperationOutcome.NotRun;
            var error = isExplicitFailure ? failure : null;
            var result = new SynchronizationOperationResult(
                runId,
                tracked.OperationId,
                tracked.Operation,
                TimeSpan.Zero,
                outcome,
                error);
            results.Add(result);
            LogOperationCompleted(result);
        }
    }

    private SynchronizationRunResult CompleteRun(
        Guid runId,
        long runStarted,
        SynchronizationRunOutcome outcome,
        IEnumerable<SynchronizationOperationResult> operations,
        SynchronizationErrorDetails? error)
    {
        var result = new SynchronizationRunResult(
            runId,
            Stopwatch.GetElapsedTime(runStarted),
            outcome,
            operations,
            error);

        if (error is null)
        {
            _logger.LogInformation(
                "Synchronization run {RunId} completed with {RunOutcome} in {DurationMilliseconds} ms.",
                runId,
                outcome,
                result.Duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Synchronization run {RunId} completed with {RunOutcome} and error {ErrorCode}; " +
                "retryable: {IsRetryable}; duration: {DurationMilliseconds} ms.",
                runId,
                outcome,
                error.Code,
                error.IsRetryable,
                result.Duration.TotalMilliseconds);
        }

        return result;
    }

    private void LogOperationCompleted(SynchronizationOperationResult result)
    {
        if (result.Error is null)
        {
            _logger.LogInformation(
                "Synchronization operation {OperationId} in run {RunId} completed with " +
                "{OperationOutcome} in {DurationMilliseconds} ms.",
                result.OperationId,
                result.RunId,
                result.Outcome,
                result.Duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Synchronization operation {OperationId} in run {RunId} completed with " +
                "{OperationOutcome} and error {ErrorCode}; retryable: {IsRetryable}; " +
                "duration: {DurationMilliseconds} ms.",
                result.OperationId,
                result.RunId,
                result.Outcome,
                result.Error.Code,
                result.Error.IsRetryable,
                result.Duration.TotalMilliseconds);
        }
    }

    private static SynchronizationErrorDetails CreateError(Exception exception) =>
        exception switch
        {
            SynchronizationExecutionException executionException => new(
                executionException.ErrorCode,
                executionException.Message,
                executionException.IsRetryable),
            StorageProviderException providerException => ProviderError(providerException),
            _ => new SynchronizationErrorDetails(
                SynchronizationErrorCode.UnexpectedFailure,
                "The synchronization operation failed unexpectedly.",
                isRetryable: false),
        };

    private static SynchronizationErrorDetails ProviderError(
        StorageProviderException exception)
    {
        var (code, message) = exception.Error switch
        {
            StorageProviderError.Unknown => (
                SynchronizationErrorCode.ProviderUnknown,
                "The storage provider reported an unknown failure."),
            StorageProviderError.NotFound => (
                SynchronizationErrorCode.ProviderNotFound,
                "A required storage entry was not found."),
            StorageProviderError.AlreadyExists => (
                SynchronizationErrorCode.ProviderAlreadyExists,
                "A storage entry already exists at the destination."),
            StorageProviderError.AccessDenied => (
                SynchronizationErrorCode.ProviderAccessDenied,
                "The storage provider denied access to an entry."),
            StorageProviderError.InvalidPath => (
                SynchronizationErrorCode.ProviderInvalidPath,
                "The storage provider rejected a path."),
            StorageProviderError.UnsupportedOperation => (
                SynchronizationErrorCode.ProviderUnsupportedOperation,
                "The storage provider does not support the operation."),
            StorageProviderError.Conflict => (
                SynchronizationErrorCode.ProviderConflict,
                "The storage provider reported a conflicting entry state."),
            StorageProviderError.RateLimited => (
                SynchronizationErrorCode.ProviderRateLimited,
                "The storage provider temporarily rate-limited the operation."),
            StorageProviderError.Offline => (
                SynchronizationErrorCode.ProviderOffline,
                "The storage provider is currently unavailable."),
            StorageProviderError.TransientFailure => (
                SynchronizationErrorCode.ProviderTransientFailure,
                "The storage provider reported a temporary failure."),
            StorageProviderError.IntegrityFailure => (
                SynchronizationErrorCode.ProviderIntegrityFailure,
                "The storage provider reported an integrity failure."),
            _ => (
                SynchronizationErrorCode.ProviderUnknown,
                "The storage provider reported an unknown failure."),
        };

        return new SynchronizationErrorDetails(code, message, exception.IsRetryable);
    }

    private static SynchronizationErrorDetails CancelledError() =>
        new(
            SynchronizationErrorCode.Cancelled,
            "The synchronization run was cancelled.",
            isRetryable: true);

    private sealed record TrackedOperation(
        SynchronizationOperation Operation,
        Guid OperationId);
}
