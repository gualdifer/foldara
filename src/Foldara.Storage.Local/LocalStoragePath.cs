using Foldara.Core;

namespace Foldara.Storage.Local;

internal static class LocalStoragePath
{
    public static string Resolve(string rootPath, StoragePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = path.IsRoot
            ? rootPath
            : Path.GetFullPath(
                path.Value.Split('/').Aggregate(rootPath, Path.Combine));
        var relativePath = Path.GetRelativePath(rootPath, fullPath);

        if (Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("The path resolves outside the local storage root.", nameof(path));
        }

        RejectSymbolicLinkSegments(rootPath, path);
        return fullPath;
    }

    public static bool IsSymbolicLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void RejectSymbolicLinkSegments(string rootPath, StoragePath path)
    {
        var currentPath = rootPath;

        foreach (var segment in path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);

            if (IsSymbolicLink(currentPath))
            {
                throw LocalStorageErrors.Create(
                    StorageProviderError.UnsupportedOperation,
                    path,
                    "Symbolic links are not supported.");
            }

            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                break;
            }
        }
    }
}
