using System.Collections.ObjectModel;

namespace Foldara.Core;

/// <summary>
/// Contains optional provider-neutral and provider-specific metadata for a storage entry.
/// </summary>
public sealed class StorageEntryMetadata
{
    private static readonly IReadOnlyDictionary<string, string> NoProviderProperties =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    public StorageEntryMetadata(
        long? size = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? lastModifiedAt = null,
        string? providerItemId = null,
        StorageContentHash? contentHash = null,
        string? revision = null,
        IReadOnlyDictionary<string, string>? providerProperties = null)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "An entry size cannot be negative.");
        }

        ValidateOptionalValue(providerItemId, nameof(providerItemId));
        ValidateOptionalValue(revision, nameof(revision));

        Size = size;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        ProviderItemId = providerItemId;
        ContentHash = contentHash;
        Revision = revision;
        ProviderProperties = CopyProviderProperties(providerProperties);
    }

    /// <summary>
    /// Gets the file size in bytes when the provider reports it.
    /// </summary>
    public long? Size { get; }

    /// <summary>
    /// Gets the creation timestamp when the provider reports it.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; }

    /// <summary>
    /// Gets the last-modified timestamp when the provider reports it.
    /// </summary>
    public DateTimeOffset? LastModifiedAt { get; }

    /// <summary>
    /// Gets the provider's stable item identifier when available.
    /// </summary>
    public string? ProviderItemId { get; }

    /// <summary>
    /// Gets the content hash when the provider reports one.
    /// </summary>
    public StorageContentHash? ContentHash { get; }

    /// <summary>
    /// Gets the provider revision, generation, or ETag when available.
    /// </summary>
    public string? Revision { get; }

    /// <summary>
    /// Gets additional non-secret provider metadata encoded as strings.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProviderProperties { get; }

    private static IReadOnlyDictionary<string, string> CopyProviderProperties(
        IReadOnlyDictionary<string, string>? providerProperties)
    {
        if (providerProperties is null || providerProperties.Count == 0)
        {
            return NoProviderProperties;
        }

        var copy = new Dictionary<string, string>(providerProperties.Count, StringComparer.Ordinal);

        foreach (var (key, value) in providerProperties)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(providerProperties));
            ArgumentNullException.ThrowIfNull(value, nameof(providerProperties));

            if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A provider property key cannot have leading or trailing whitespace.",
                    nameof(providerProperties));
            }

            copy.Add(key, value);
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }

    private static void ValidateOptionalValue(string? value, string parameterName)
    {
        if (value is null)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The value cannot have leading or trailing whitespace.",
                parameterName);
        }
    }
}
