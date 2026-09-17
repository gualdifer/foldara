using System.Runtime.CompilerServices;
using Foldara.Core;
using Foldara.Storage;
using Foldara.Storage.Local;
using Foldara.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Foldara.Engine.Tests;

public sealed class SynchronizationPlanExecutorTests
{
    [Fact]
    public async Task ExecuteCreatesDirectoriesAndTransfersFiles()
    {
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("new/file.txt"), "new")
            .WriteTextFile(StoragePath.Parse("replace.txt"), "replacement");
        using var destinationDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("replace.txt"), "original");
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.CreateDirectory(
                StoragePath.Parse("new"),
                "Directory is missing."),
            SynchronizationOperation.Copy(
                StoragePath.Parse("new/file.txt"),
                StoragePath.Parse("new/file.txt"),
                "File is missing."),
            SynchronizationOperation.Replace(
                StoragePath.Parse("replace.txt"),
                StoragePath.Parse("replace.txt"),
                "File changed."),
        ]);
        var executor = new SynchronizationPlanExecutor();

        var result = await executor.ExecuteAsync(
            plan,
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(SynchronizationRunOutcome.Succeeded, result.Outcome);
        Assert.All(
            result.Operations,
            operation => Assert.Equal(
                SynchronizationOperationOutcome.Succeeded,
                operation.Outcome));
        Assert.Equal(
            "new",
            File.ReadAllText(destinationDirectory.GetFullPath(StoragePath.Parse("new/file.txt"))));
        Assert.Equal(
            "replacement",
            File.ReadAllText(destinationDirectory.GetFullPath(StoragePath.Parse("replace.txt"))));
    }

    [Fact]
    public async Task ExecuteCanRepeatACompletedCopyPlan()
    {
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "contents");
        using var destinationDirectory = TemporaryDirectory.Create();
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var plan = CopyPlan("file.txt");
        var executor = new SynchronizationPlanExecutor();
        var cancellationToken = TestContext.Current.CancellationToken;

        await executor.ExecuteAsync(plan, source, destination, cancellationToken);
        await executor.ExecuteAsync(plan, source, destination, cancellationToken);

        Assert.Equal(
            "contents",
            File.ReadAllText(destinationDirectory.GetFullPath(StoragePath.Parse("file.txt"))));
    }

    [Fact]
    public async Task ExecuteRefusesToOverwriteDifferentContentForCopy()
    {
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "source");
        using var destinationDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "changed");
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var executor = new SynchronizationPlanExecutor();

        var result = await executor.ExecuteAsync(
            CopyPlan("file.txt"),
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(SynchronizationRunOutcome.Failed, result.Outcome);
        Assert.Equal(SynchronizationErrorCode.DestinationConflict, result.Error?.Code);
        Assert.False(result.Error?.IsRetryable);
        Assert.Equal(
            "changed",
            File.ReadAllText(destinationDirectory.GetFullPath(StoragePath.Parse("file.txt"))));
    }

    [Fact]
    public async Task ExecuteRejectsConflictsBeforeAnyMutation()
    {
        using var sourceDirectory = TemporaryDirectory.Create();
        using var destinationDirectory = TemporaryDirectory.Create();
        var source = new LocalStorageProvider(sourceDirectory.RootPath);
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var conflictPath = StoragePath.Parse("conflict");
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.CreateDirectory(
                StoragePath.Parse("must-not-exist"),
                "Directory is missing."),
            SynchronizationOperation.Conflict(
                conflictPath,
                conflictPath,
                "Conflict."),
        ]);
        var executor = new SynchronizationPlanExecutor();

        var result = await executor.ExecuteAsync(
            plan,
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(SynchronizationRunOutcome.Failed, result.Outcome);
        Assert.Equal(SynchronizationErrorCode.PlanConflict, result.Error?.Code);
        Assert.Equal(
            [SynchronizationOperationOutcome.NotRun, SynchronizationOperationOutcome.Failed],
            result.Operations.Select(operation => operation.Outcome));
        Assert.False(Directory.Exists(
            destinationDirectory.GetFullPath(StoragePath.Parse("must-not-exist"))));
    }

    [Fact]
    public async Task ExecuteLeavesDeleteCandidatesUntouched()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("preserved.txt"), "contents");
        var provider = new LocalStorageProvider(directory.RootPath);
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.DeleteCandidate(
                StoragePath.Parse("preserved.txt"),
                "Deletion is disabled."),
        ]);
        var executor = new SynchronizationPlanExecutor();

        await executor.ExecuteAsync(
            plan,
            provider,
            provider,
            TestContext.Current.CancellationToken);

        Assert.True(File.Exists(directory.GetFullPath(StoragePath.Parse("preserved.txt"))));
    }

    [Fact]
    public async Task ExecuteCanRepeatACompletedMove()
    {
        using var directory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("old.txt"), "contents");
        var provider = new LocalStorageProvider(directory.RootPath);
        var plan = new SynchronizationPlan(
        [
            SynchronizationOperation.Move(
                StoragePath.Parse("old.txt"),
                StoragePath.Parse("new.txt"),
                "Entry was renamed."),
        ]);
        var executor = new SynchronizationPlanExecutor();
        var cancellationToken = TestContext.Current.CancellationToken;

        await executor.ExecuteAsync(plan, provider, provider, cancellationToken);
        await executor.ExecuteAsync(plan, provider, provider, cancellationToken);

        Assert.False(File.Exists(directory.GetFullPath(StoragePath.Parse("old.txt"))));
        Assert.Equal(
            "contents",
            File.ReadAllText(directory.GetFullPath(StoragePath.Parse("new.txt"))));
    }

    [Fact]
    public async Task TransferFailureDoesNotPublishPartialDestination()
    {
        using var destinationDirectory = TemporaryDirectory.Create();
        var source = new FailingReadProvider();
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var executor = new SynchronizationPlanExecutor();

        var result = await executor.ExecuteAsync(
            CopyPlan("file.bin"),
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(SynchronizationRunOutcome.Failed, result.Outcome);
        Assert.Equal(SynchronizationErrorCode.ProviderTransientFailure, result.Error?.Code);
        Assert.True(result.Error?.IsRetryable);
        Assert.Empty(destinationDirectory.GetTreeSnapshot());
    }

    [Fact]
    public async Task ExecuteRejectsSourceModifiedDuringTransferAsRetryable()
    {
        var path = StoragePath.Parse("file.txt");
        var initialTimestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var source = new ObservedSourceProvider(
            path,
            "updated"u8.ToArray(),
            new StorageEntryMetadata(size: 7, lastModifiedAt: initialTimestamp),
            new StorageEntryMetadata(size: 7, lastModifiedAt: initialTimestamp.AddSeconds(1)));
        using var destinationDirectory = TemporaryDirectory.Create()
            .WriteTextFile(path, "original");
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var operation = SynchronizationOperation.Replace(path, path, "File changed.");
        var plan = new SynchronizationPlan([operation]);
        var executor = new SynchronizationPlanExecutor();

        var result = await executor.ExecuteAsync(
            plan,
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Same(operation, result.Operations[0].Operation);
        Assert.Equal(SynchronizationErrorCode.SourceChanged, result.Error?.Code);
        Assert.True(result.Error?.IsRetryable);
        Assert.Equal(
            "original",
            File.ReadAllText(destinationDirectory.GetFullPath(path)));
        Assert.Equal(["file.txt"], destinationDirectory.GetTreeSnapshot());
    }

    [Fact]
    public async Task ExecuteRejectsAReadLengthThatDoesNotMatchSourceMetadata()
    {
        var path = StoragePath.Parse("file.txt");
        var metadata = new StorageEntryMetadata(size: 8);
        var source = new ObservedSourceProvider(
            path,
            "short"u8.ToArray(),
            metadata,
            metadata);
        using var destinationDirectory = TemporaryDirectory.Create();
        var destination = new LocalStorageProvider(destinationDirectory.RootPath);
        var executor = new SynchronizationPlanExecutor();

        var result = await executor.ExecuteAsync(
            CopyPlan("file.txt"),
            source,
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(SynchronizationErrorCode.SourceChanged, result.Error?.Code);
        Assert.True(result.Error?.IsRetryable);
        Assert.Empty(destinationDirectory.GetTreeSnapshot());
    }

    [Fact]
    public async Task ExecuteHonorsPreCancelledTokenWithoutMutation()
    {
        using var sourceDirectory = TemporaryDirectory.Create()
            .WriteTextFile(StoragePath.Parse("file.txt"), "contents");
        using var destinationDirectory = TemporaryDirectory.Create();
        var executor = new SynchronizationPlanExecutor();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await executor.ExecuteAsync(
            CopyPlan("file.txt"),
            new LocalStorageProvider(sourceDirectory.RootPath),
            new LocalStorageProvider(destinationDirectory.RootPath),
            cancellation.Token);

        Assert.Equal(SynchronizationRunOutcome.Cancelled, result.Outcome);
        Assert.Equal(SynchronizationErrorCode.Cancelled, result.Error?.Code);
        Assert.Equal(SynchronizationOperationOutcome.NotRun, result.Operations[0].Outcome);
        Assert.Empty(destinationDirectory.GetTreeSnapshot());
    }

    [Fact]
    public async Task ExecuteReturnsCorrelatedResultsAndStructuredSanitizedLogs()
    {
        const string SensitiveProviderDetail = "/private/source/secret.txt";
        using var destinationDirectory = TemporaryDirectory.Create();
        var logger = new RecordingLogger<SynchronizationPlanExecutor>();
        var executor = new SynchronizationPlanExecutor(logger);

        var result = await executor.ExecuteAsync(
            CopyPlan("file.bin"),
            new FailingReadProvider(SensitiveProviderDetail),
            new LocalStorageProvider(destinationDirectory.RootPath),
            TestContext.Current.CancellationToken);

        var operation = Assert.Single(result.Operations);
        Assert.NotEqual(Guid.Empty, result.RunId);
        Assert.NotEqual(Guid.Empty, operation.OperationId);
        Assert.Equal(result.RunId, operation.RunId);
        Assert.True(result.Duration >= operation.Duration);
        Assert.Equal(SynchronizationRunOutcome.Failed, result.Outcome);
        Assert.Equal(SynchronizationOperationOutcome.Failed, operation.Outcome);
        Assert.Equal(SynchronizationErrorCode.ProviderTransientFailure, operation.Error?.Code);
        Assert.DoesNotContain(SensitiveProviderDetail, result.Error?.Message, StringComparison.Ordinal);
        Assert.Contains(
            logger.Entries,
            entry => entry.Properties.TryGetValue("RunId", out var value) &&
                Equals(value, result.RunId));
        Assert.Contains(
            logger.Entries,
            entry => entry.Properties.TryGetValue("OperationId", out var value) &&
                Equals(value, operation.OperationId));
        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Message.Contains(SensitiveProviderDetail, StringComparison.Ordinal));
    }

    private static SynchronizationPlan CopyPlan(string path)
    {
        var storagePath = StoragePath.Parse(path);
        return new SynchronizationPlan(
        [
            SynchronizationOperation.Copy(storagePath, storagePath, "File is missing."),
        ]);
    }

    private sealed class FailingReadProvider : IStorageProvider
    {
        private static readonly StoragePath FilePath = StoragePath.Parse("file.bin");
        private static readonly StorageEntry FileEntry = new(
            FilePath,
            StorageEntryKind.File,
            new StorageEntryMetadata(size: 100_000));
        private readonly string _failureMessage;

        public FailingReadProvider(string failureMessage = "Configured read failure.")
        {
            _failureMessage = failureMessage;
        }

        public StorageProviderCapabilities Capabilities { get; } =
            new(StoragePathCaseSensitivity.Sensitive);

        public ValueTask<StorageEntry?> GetEntryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<StorageEntry?>(path == FilePath ? FileEntry : null);
        }

        public async IAsyncEnumerable<StorageEntry> EnumerateChildrenAsync(
            StoragePath directory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }

        public ValueTask<Stream> OpenReadAsync(
            StoragePath path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new FailingReadStream(_failureMessage));
        }

        public ValueTask<IStorageWriteSession> BeginWriteAsync(
            StoragePath path,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask CreateDirectoryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask MoveAsync(
            StoragePath source,
            StoragePath destination,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DeleteAsync(
            StoragePath path,
            StorageDeleteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ObservedSourceProvider : IStorageProvider
    {
        private readonly StorageEntry _after;
        private readonly StorageEntry _before;
        private readonly byte[] _contents;
        private readonly StoragePath _path;
        private int _observationCount;

        public ObservedSourceProvider(
            StoragePath path,
            byte[] contents,
            StorageEntryMetadata before,
            StorageEntryMetadata after)
        {
            _path = path;
            _contents = contents;
            _before = new StorageEntry(path, StorageEntryKind.File, before);
            _after = new StorageEntry(path, StorageEntryKind.File, after);
        }

        public StorageProviderCapabilities Capabilities { get; } =
            new(StoragePathCaseSensitivity.Sensitive);

        public ValueTask<StorageEntry?> GetEntryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path != _path)
            {
                return ValueTask.FromResult<StorageEntry?>(null);
            }

            var observation = Interlocked.Increment(ref _observationCount) == 1
                ? _before
                : _after;
            return ValueTask.FromResult<StorageEntry?>(observation);
        }

        public async IAsyncEnumerable<StorageEntry> EnumerateChildrenAsync(
            StoragePath directory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }

        public ValueTask<Stream> OpenReadAsync(
            StoragePath path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(_contents, writable: false));
        }

        public ValueTask<IStorageWriteSession> BeginWriteAsync(
            StoragePath path,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask CreateDirectoryAsync(
            StoragePath path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask MoveAsync(
            StoragePath source,
            StoragePath destination,
            StorageWriteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DeleteAsync(
            StoragePath path,
            StorageDeleteMode mode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FailingReadStream : MemoryStream
    {
        private readonly string _failureMessage;
        private int _readCount;

        public FailingReadStream(string failureMessage)
            : base(new byte[100_000], writable: false)
        {
            _failureMessage = failureMessage;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_readCount++ > 0)
            {
                throw new StorageProviderException(
                    StorageProviderError.TransientFailure,
                    _failureMessage);
            }

            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);
}
