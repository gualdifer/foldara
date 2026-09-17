using Foldara.Core;

namespace Foldara.Storage;

/// <summary>
/// Provides provider-neutral access to one configured storage root.
/// </summary>
/// <remarks>
/// Implementations must honor cancellation and must not wrap
/// <see cref="OperationCanceledException"/>. Provider failures must be translated to
/// <see cref="StorageProviderException"/>; argument and object-lifetime failures retain their
/// standard .NET exception types. Returned streams and write sessions are owned by the caller.
/// Unsupported optional operations must fail with
/// <see cref="StorageProviderError.UnsupportedOperation"/>.
/// </remarks>
public interface IStorageProvider
{
    /// <summary>
    /// Gets the capabilities for this configured storage root.
    /// </summary>
    StorageProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Gets metadata for one path, or <see langword="null"/> when it does not exist.
    /// </summary>
    ValueTask<StorageEntry?> GetEntryAsync(
        StoragePath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the direct children of a directory without recursively traversing descendants.
    /// </summary>
    IAsyncEnumerable<StorageEntry> EnumerateChildrenAsync(
        StoragePath directory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file for streaming reads.
    /// </summary>
    ValueTask<Stream> OpenReadAsync(
        StoragePath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a streaming write that is invisible or incomplete until explicitly committed.
    /// </summary>
    ValueTask<IStorageWriteSession> BeginWriteAsync(
        StoragePath path,
        StorageWriteMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a directory. The operation succeeds when that directory already exists.
    /// </summary>
    ValueTask CreateDirectoryAsync(
        StoragePath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an entry when <see cref="StorageProviderCapabilities.SupportsMove"/> is true.
    /// </summary>
    ValueTask MoveAsync(
        StoragePath source,
        StoragePath destination,
        StorageWriteMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entry when <see cref="StorageProviderCapabilities.SupportsDelete"/> is true.
    /// Missing entries are treated as already deleted.
    /// </summary>
    ValueTask DeleteAsync(
        StoragePath path,
        StorageDeleteMode mode,
        CancellationToken cancellationToken = default);
}
