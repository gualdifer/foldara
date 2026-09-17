using System.Collections.ObjectModel;
using System.Text;
using Foldara.Core;

namespace Foldara.Testing;

/// <summary>
/// Owns an isolated temporary directory for one test and deletes it on disposal.
/// </summary>
public sealed class TemporaryDirectory : IDisposable
{
    private bool _isDisposed;

    private TemporaryDirectory(string rootPath)
    {
        RootPath = rootPath;
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Gets the absolute path of the temporary root.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Creates a uniquely named temporary directory.
    /// </summary>
    public static TemporaryDirectory Create()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "Foldara.Tests",
            Guid.NewGuid().ToString("N"));

        return new TemporaryDirectory(rootPath);
    }

    /// <summary>
    /// Creates a directory and any missing parents beneath this fixture root.
    /// </summary>
    public TemporaryDirectory CreateDirectory(StoragePath path)
    {
        Directory.CreateDirectory(GetFullPath(path));
        return this;
    }

    /// <summary>
    /// Creates or replaces a UTF-8 text file beneath this fixture root.
    /// </summary>
    public TemporaryDirectory WriteTextFile(StoragePath path, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        return WriteFile(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(contents));
    }

    /// <summary>
    /// Creates or replaces a binary file beneath this fixture root.
    /// </summary>
    public TemporaryDirectory WriteFile(StoragePath path, ReadOnlySpan<byte> contents)
    {
        var fullPath = GetFilePath(path);
        var parentPath = Path.GetDirectoryName(fullPath);

        if (parentPath is not null)
        {
            Directory.CreateDirectory(parentPath);
        }

        File.WriteAllBytes(fullPath, contents);
        return this;
    }

    /// <summary>
    /// Resolves a normalized storage path beneath this fixture root.
    /// </summary>
    public string GetFullPath(StoragePath path)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(path);

        if (path.IsRoot)
        {
            return RootPath;
        }

        return path.Value
            .Split('/')
            .Aggregate(RootPath, Path.Combine);
    }

    /// <summary>
    /// Captures relative file and directory paths in deterministic ordinal order.
    /// Directory entries end in <c>/</c>.
    /// </summary>
    public IReadOnlyList<string> GetTreeSnapshot()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var entries = Directory
            .EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(RootPath, path)
                    .Replace(Path.DirectorySeparatorChar, '/');

                return Directory.Exists(path) ? $"{relativePath}/" : relativePath;
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new ReadOnlyCollection<string>(entries);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private string GetFilePath(StoragePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.IsRoot)
        {
            throw new ArgumentException("The fixture root cannot be used as a file path.", nameof(path));
        }

        return GetFullPath(path);
    }
}
