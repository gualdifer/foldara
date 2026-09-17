using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Foldara.Core;

namespace Foldara.Storage.Local;

/// <summary>
/// Provides storage access beneath one local file-system directory.
/// </summary>
public sealed class LocalStorageProvider : IStorageProvider
{
    private readonly ConcurrentDictionary<string, byte> _activeStagingPaths;
    private readonly StringComparer _pathComparer;
    private readonly string _rootPath;

    public LocalStorageProvider(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootPath = Path.GetFullPath(rootPath);

        if (!Directory.Exists(_rootPath))
        {
            throw new DirectoryNotFoundException("The local storage root does not exist.");
        }

        if (LocalStoragePath.IsSymbolicLink(_rootPath))
        {
            throw new ArgumentException(
                "A local storage root cannot be a symbolic link.",
                nameof(rootPath));
        }

        _pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        _activeStagingPaths = new ConcurrentDictionary<string, byte>(_pathComparer);
    }

    public StorageProviderCapabilities Capabilities { get; } = new(
        GetPathCaseSensitivity(),
        supportsMove: true,
        supportsDelete: true);

    public ValueTask<StorageEntry?> GetEntryAsync(
        StoragePath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fullPath = ResolvePath(path);

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return ValueTask.FromResult<StorageEntry?>(null);
            }

            return ValueTask.FromResult<StorageEntry?>(CreateEntry(path, fullPath));
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, path);
        }
    }

    public async IAsyncEnumerable<StorageEntry> EnumerateChildrenAsync(
        StoragePath directory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string directoryPath;
        IEnumerator<string> enumerator;

        try
        {
            directoryPath = ResolvePath(directory);

            if (!Directory.Exists(directoryPath))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.NotFound,
                    directory,
                    "The directory does not exist.");
            }

            enumerator = Directory
                .EnumerateFileSystemEntries(directoryPath)
                .GetEnumerator();
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, directory);
        }

        using (enumerator)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StorageEntry? entry;

                try
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }

                    var childPath = enumerator.Current;

                    if (_activeStagingPaths.ContainsKey(childPath))
                    {
                        continue;
                    }

                    var childStoragePath = directory.Append(Path.GetFileName(childPath));
                    entry = CreateEntry(childStoragePath, childPath);
                }
                catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
                {
                    throw LocalStorageErrors.Translate(exception, directory);
                }

                yield return entry;
                await Task.Yield();
            }
        }
    }

    public ValueTask<Stream> OpenReadAsync(
        StoragePath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fullPath = ResolvePath(path);
            Stream stream = new FileStream(
                fullPath,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    BufferSize = 81920,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });

            return ValueTask.FromResult(stream);
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, path);
        }
    }

    public ValueTask<IStorageWriteSession> BeginWriteAsync(
        StoragePath path,
        StorageWriteMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown write mode.");
        }

        if (path.IsRoot)
        {
            throw LocalStorageErrors.Create(
                StorageProviderError.InvalidPath,
                path,
                "The storage root cannot be opened as a file.");
        }

        try
        {
            var destinationPath = ResolvePath(path);
            var parentPath = Path.GetDirectoryName(destinationPath)!;

            if (!Directory.Exists(parentPath))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.NotFound,
                    path.Parent ?? StoragePath.Root,
                    "The destination directory does not exist.");
            }

            IStorageWriteSession session = LocalStorageWriteSession.Create(
                destinationPath,
                path,
                mode,
                RegisterStagingPath,
                UnregisterStagingPath);

            return ValueTask.FromResult(session);
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, path);
        }
    }

    public ValueTask CreateDirectoryAsync(
        StoragePath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fullPath = ResolvePath(path);

            if (File.Exists(fullPath))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.AlreadyExists,
                    path,
                    "A file already exists at the directory path.");
            }

            Directory.CreateDirectory(fullPath);
            return ValueTask.CompletedTask;
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, path);
        }
    }

    public ValueTask MoveAsync(
        StoragePath source,
        StoragePath destination,
        StorageWriteMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown write mode.");
        }

        if (source.IsRoot || destination.IsRoot)
        {
            throw LocalStorageErrors.Create(
                StorageProviderError.InvalidPath,
                source.IsRoot ? source : destination,
                "The storage root cannot be moved or replaced.");
        }

        try
        {
            var sourcePath = ResolvePath(source);
            var destinationPath = ResolvePath(destination);

            if (mode == StorageWriteMode.CreateNew &&
                (File.Exists(destinationPath) || Directory.Exists(destinationPath)))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.AlreadyExists,
                    destination,
                    "The destination entry already exists.");
            }

            if (File.Exists(sourcePath))
            {
                if (Directory.Exists(destinationPath))
                {
                    throw LocalStorageErrors.Create(
                        StorageProviderError.AlreadyExists,
                        destination,
                        "A directory already exists at the destination.");
                }

                File.Move(
                    sourcePath,
                    destinationPath,
                    overwrite: mode == StorageWriteMode.CreateOrReplace);
            }
            else if (Directory.Exists(sourcePath))
            {
                if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
                {
                    throw LocalStorageErrors.Create(
                        mode == StorageWriteMode.CreateNew
                            ? StorageProviderError.AlreadyExists
                            : StorageProviderError.UnsupportedOperation,
                        destination,
                        "Replacing a directory during a move is not supported.");
                }

                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.NotFound,
                    source,
                    "The source entry does not exist.");
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, source);
        }
    }

    public ValueTask DeleteAsync(
        StoragePath path,
        StorageDeleteMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown delete mode.");
        }

        if (mode == StorageDeleteMode.Trash)
        {
            throw LocalStorageErrors.Create(
                StorageProviderError.UnsupportedOperation,
                path,
                "The local provider does not support trash operations.");
        }

        if (path.IsRoot)
        {
            throw LocalStorageErrors.Create(
                StorageProviderError.InvalidPath,
                path,
                "The storage root cannot be deleted.");
        }

        try
        {
            var fullPath = ResolvePath(path);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                try
                {
                    Directory.Delete(fullPath, recursive: false);
                }
                catch (IOException)
                {
                    throw LocalStorageErrors.Create(
                        StorageProviderError.Conflict,
                        path,
                        "The directory is not empty or is currently in use.");
                }
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception exception) when (LocalStorageErrors.ShouldTranslate(exception))
        {
            throw LocalStorageErrors.Translate(exception, path);
        }
    }

    private static StoragePathCaseSensitivity GetPathCaseSensitivity()
    {
        if (OperatingSystem.IsWindows())
        {
            return StoragePathCaseSensitivity.Insensitive;
        }

        return OperatingSystem.IsMacOS()
            ? StoragePathCaseSensitivity.ProviderDependent
            : StoragePathCaseSensitivity.Sensitive;
    }

    private static StorageEntry CreateEntry(StoragePath path, string fullPath)
    {
        if (LocalStoragePath.IsSymbolicLink(fullPath))
        {
            throw LocalStorageErrors.Create(
                StorageProviderError.UnsupportedOperation,
                path,
                "Symbolic links are not supported.");
        }

        if (Directory.Exists(fullPath))
        {
            var directory = new DirectoryInfo(fullPath);
            directory.Refresh();

            return new StorageEntry(
                path,
                StorageEntryKind.Directory,
                new StorageEntryMetadata(
                    createdAt: ToDateTimeOffset(directory.CreationTimeUtc),
                    lastModifiedAt: ToDateTimeOffset(directory.LastWriteTimeUtc)));
        }

        var file = new FileInfo(fullPath);
        file.Refresh();

        if (!file.Exists)
        {
            throw new FileNotFoundException("The local entry no longer exists.");
        }

        return new StorageEntry(
            path,
            StorageEntryKind.File,
            new StorageEntryMetadata(
                size: file.Length,
                createdAt: ToDateTimeOffset(file.CreationTimeUtc),
                lastModifiedAt: ToDateTimeOffset(file.LastWriteTimeUtc)));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime utcValue) =>
        new(DateTime.SpecifyKind(utcValue, DateTimeKind.Utc));

    private string ResolvePath(StoragePath path) =>
        LocalStoragePath.Resolve(_rootPath, path);

    private void RegisterStagingPath(string path) => _activeStagingPaths.TryAdd(path, 0);

    private void UnregisterStagingPath(string path) => _activeStagingPaths.TryRemove(path, out _);
}
