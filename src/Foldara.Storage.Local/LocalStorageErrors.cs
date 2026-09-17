using System.Globalization;
using System.Security;
using System.Text;
using Foldara.Core;

namespace Foldara.Storage.Local;

internal static class LocalStorageErrors
{
    public static bool ShouldTranslate(Exception exception) =>
        exception is StorageProviderException
            or IOException
            or UnauthorizedAccessException
            or SecurityException
            or ArgumentException
            or NotSupportedException;

    public static StorageProviderException Translate(Exception exception, StoragePath path)
    {
        if (exception is StorageProviderException providerException)
        {
            return providerException;
        }

        var error = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => StorageProviderError.NotFound,
            UnauthorizedAccessException or SecurityException => StorageProviderError.AccessDenied,
            ArgumentException or NotSupportedException or PathTooLongException =>
                StorageProviderError.InvalidPath,
            IOException => StorageProviderError.Unknown,
            _ => StorageProviderError.Unknown,
        };

        return Create(error, path, "The local file-system operation failed.");
    }

    public static StorageProviderException Create(
        StorageProviderError error,
        StoragePath path,
        string message) =>
        new(error, $"{message} Path: '{SanitizePath(path.Value)}'.");

    private static string SanitizePath(string path)
    {
        var sanitized = new StringBuilder(path.Length);

        foreach (var character in path)
        {
            if (char.IsControl(character))
            {
                sanitized.Append("\\u");
                sanitized.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                sanitized.Append(character);
            }
        }

        return sanitized.ToString();
    }
}
