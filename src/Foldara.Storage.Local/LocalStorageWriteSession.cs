using Foldara.Core;

namespace Foldara.Storage.Local;

internal sealed class LocalStorageWriteSession : IStorageWriteSession
{
    private readonly StoragePath _destinationStoragePath;
    private readonly string _destinationPath;
    private readonly StorageWriteMode _mode;
    private readonly Action<string> _unregisterStagingPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FileStream _stream;
    private readonly string _stagingPath;
    private bool _isCommitted;
    private bool _isDisposed;

    private LocalStorageWriteSession(
        string destinationPath,
        StoragePath destinationStoragePath,
        StorageWriteMode mode,
        string stagingPath,
        FileStream stream,
        Action<string> unregisterStagingPath)
    {
        _destinationPath = destinationPath;
        _destinationStoragePath = destinationStoragePath;
        _mode = mode;
        _stagingPath = stagingPath;
        _stream = stream;
        _unregisterStagingPath = unregisterStagingPath;
    }

    public Stream Content
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return _stream;
        }
    }

    public static LocalStorageWriteSession Create(
        string destinationPath,
        StoragePath destinationStoragePath,
        StorageWriteMode mode,
        Action<string> registerStagingPath,
        Action<string> unregisterStagingPath)
    {
        var directoryPath = Path.GetDirectoryName(destinationPath)!;

        while (true)
        {
            var stagingPath = Path.Combine(
                directoryPath,
                $".foldara-write-{Guid.NewGuid():N}.tmp");

            registerStagingPath(stagingPath);

            try
            {
                var stream = new FileStream(
                    stagingPath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        BufferSize = 81920,
                        Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    });

                return new LocalStorageWriteSession(
                    destinationPath,
                    destinationStoragePath,
                    mode,
                    stagingPath,
                    stream,
                    unregisterStagingPath);
            }
            catch (IOException) when (File.Exists(stagingPath))
            {
                unregisterStagingPath(stagingPath);
            }
            catch
            {
                unregisterStagingPath(stagingPath);
                throw;
            }
        }
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isCommitted)
            {
                return;
            }

            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _stream.Flush(flushToDisk: true);
            await _stream.DisposeAsync().ConfigureAwait(false);

            if (Directory.Exists(_destinationPath))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.AlreadyExists,
                    _destinationStoragePath,
                    "A directory already exists at the destination.");
            }

            try
            {
                File.Move(
                    _stagingPath,
                    _destinationPath,
                    overwrite: _mode == StorageWriteMode.CreateOrReplace);
            }
            catch (IOException) when (
                _mode == StorageWriteMode.CreateNew && File.Exists(_destinationPath))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.AlreadyExists,
                    _destinationStoragePath,
                    "The destination file already exists.");
            }

            _isCommitted = true;
            _unregisterStagingPath(_stagingPath);
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, _destinationStoragePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                await _stream.DisposeAsync().ConfigureAwait(false);

                if (!_isCommitted && File.Exists(_stagingPath))
                {
                    File.Delete(_stagingPath);
                }
            }
            finally
            {
                _unregisterStagingPath(_stagingPath);
            }
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, _destinationStoragePath);
        }
        finally
        {
            _gate.Release();
        }
    }
}
