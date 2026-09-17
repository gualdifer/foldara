namespace Foldara.Storage;

/// <summary>
/// Owns a streaming provider write that remains uncommitted until explicitly completed.
/// </summary>
/// <remarks>
/// Disposing a session before <see cref="CommitAsync"/> completes must abandon the write.
/// A caller must not use <see cref="Content"/> after committing or disposing the session.
/// </remarks>
public interface IStorageWriteSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the writable stream owned by this session.
    /// </summary>
    Stream Content { get; }

    /// <summary>
    /// Commits the completed stream to its destination.
    /// </summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
