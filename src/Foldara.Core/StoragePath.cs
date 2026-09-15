using System.Diagnostics.CodeAnalysis;

namespace Foldara.Core;

/// <summary>
/// Represents a provider-independent path relative to a storage root.
/// </summary>
public sealed class StoragePath : IEquatable<StoragePath>
{
    private const char Separator = '/';

    private StoragePath(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the storage root.
    /// </summary>
    public static StoragePath Root { get; } = new(string.Empty);

    /// <summary>
    /// Gets the normalized path, using <c>/</c> as its separator.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets a value indicating whether this path represents the storage root.
    /// </summary>
    public bool IsRoot => Value.Length == 0;

    /// <summary>
    /// Gets the final path segment, or an empty string for the storage root.
    /// </summary>
    public string Name
    {
        get
        {
            if (IsRoot)
            {
                return string.Empty;
            }

            var separatorIndex = Value.LastIndexOf(Separator);
            return separatorIndex < 0 ? Value : Value[(separatorIndex + 1)..];
        }
    }

    /// <summary>
    /// Gets the parent path, or <see langword="null"/> for the storage root.
    /// </summary>
    public StoragePath? Parent
    {
        get
        {
            if (IsRoot)
            {
                return null;
            }

            var separatorIndex = Value.LastIndexOf(Separator);
            return separatorIndex < 0 ? Root : new StoragePath(Value[..separatorIndex]);
        }
    }

    /// <summary>
    /// Parses a provider-independent relative path.
    /// </summary>
    /// <param name="value">The path to parse. An empty string represents the storage root.</param>
    /// <returns>The parsed path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException"><paramref name="value"/> is not a valid storage path.</exception>
    public static StoragePath Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            return Root;
        }

        ValidatePath(value);
        return new StoragePath(value);
    }

    /// <summary>
    /// Attempts to parse a provider-independent relative path.
    /// </summary>
    public static bool TryParse(
        string? value,
        [NotNullWhen(true)] out StoragePath? path)
    {
        if (value is null)
        {
            path = null;
            return false;
        }

        try
        {
            path = Parse(value);
            return true;
        }
        catch (FormatException)
        {
            path = null;
            return false;
        }
    }

    /// <summary>
    /// Appends one path segment.
    /// </summary>
    public StoragePath Append(string segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ValidateSegment(segment);

        return IsRoot
            ? new StoragePath(segment)
            : new StoragePath($"{Value}{Separator}{segment}");
    }

    public bool Equals(StoragePath? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as StoragePath);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(StoragePath? left, StoragePath? right) =>
        Equals(left, right);

    public static bool operator !=(StoragePath? left, StoragePath? right) =>
        !Equals(left, right);

    private static void ValidatePath(string value)
    {
        if (value[0] == Separator)
        {
            throw new FormatException("A storage path must be relative to its storage root.");
        }

        if (value.Contains('\\', StringComparison.Ordinal))
        {
            throw new FormatException("A storage path must use '/' as its separator.");
        }

        foreach (var segment in value.Split(Separator))
        {
            ValidateSegment(segment);
        }
    }

    private static void ValidateSegment(string segment)
    {
        if (segment.Length == 0)
        {
            throw new FormatException("A storage path cannot contain empty segments.");
        }

        if (segment is "." or "..")
        {
            throw new FormatException("A storage path cannot contain traversal segments.");
        }

        if (segment.Contains(Separator, StringComparison.Ordinal) ||
            segment.Contains('\\', StringComparison.Ordinal))
        {
            throw new FormatException("A path segment cannot contain a directory separator.");
        }

        if (segment.Contains('\0', StringComparison.Ordinal))
        {
            throw new FormatException("A path segment cannot contain a null character.");
        }
    }
}
