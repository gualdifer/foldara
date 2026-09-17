namespace Foldara.Core;

/// <summary>
/// Identifies file content using a provider-reported hashing algorithm and value.
/// </summary>
public sealed record StorageContentHash
{
    public StorageContentHash(string algorithm, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.Equals(algorithm, algorithm.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A hash algorithm cannot have leading or trailing whitespace.", nameof(algorithm));
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A hash value cannot have leading or trailing whitespace.", nameof(value));
        }

        Algorithm = algorithm;
        Value = value;
    }

    /// <summary>
    /// Gets the algorithm name, such as <c>SHA-256</c> or <c>MD5</c>.
    /// </summary>
    public string Algorithm { get; }

    /// <summary>
    /// Gets the provider-reported hash value without changing its encoding or casing.
    /// </summary>
    public string Value { get; }
}
