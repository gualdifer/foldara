namespace Foldara.Core;

/// <summary>
/// Represents one immutable action or decision in a synchronization plan.
/// </summary>
public sealed class SynchronizationOperation
{
    private SynchronizationOperation(
        SynchronizationOperationKind kind,
        StoragePath? sourcePath,
        StoragePath? destinationPath,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!string.Equals(reason, reason.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An operation reason cannot have leading or trailing whitespace.",
                nameof(reason));
        }

        Kind = kind;
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Reason = reason;
    }

    /// <summary>
    /// Gets the planned action or decision.
    /// </summary>
    public SynchronizationOperationKind Kind { get; }

    /// <summary>
    /// Gets the source path when the operation refers to a source entry.
    /// </summary>
    public StoragePath? SourcePath { get; }

    /// <summary>
    /// Gets the destination path when the operation refers to a destination entry.
    /// </summary>
    public StoragePath? DestinationPath { get; }

    /// <summary>
    /// Gets a sanitized explanation suitable for dry-run output and diagnostics.
    /// </summary>
    public string Reason { get; }

    public static SynchronizationOperation Copy(
        StoragePath sourcePath,
        StoragePath destinationPath,
        string reason)
    {
        ValidatePaths(sourcePath, destinationPath);
        return new(SynchronizationOperationKind.Copy, sourcePath, destinationPath, reason);
    }

    public static SynchronizationOperation CreateDirectory(
        StoragePath destinationPath,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(destinationPath);
        return new(SynchronizationOperationKind.CreateDirectory, null, destinationPath, reason);
    }

    public static SynchronizationOperation Replace(
        StoragePath sourcePath,
        StoragePath destinationPath,
        string reason)
    {
        ValidatePaths(sourcePath, destinationPath);
        return new(SynchronizationOperationKind.Replace, sourcePath, destinationPath, reason);
    }

    /// <summary>
    /// Creates a move within the destination storage root.
    /// </summary>
    public static SynchronizationOperation Move(
        StoragePath currentDestinationPath,
        StoragePath newDestinationPath,
        string reason)
    {
        ValidatePaths(currentDestinationPath, newDestinationPath);
        return new(
            SynchronizationOperationKind.Move,
            currentDestinationPath,
            newDestinationPath,
            reason);
    }

    public static SynchronizationOperation Skip(
        StoragePath? sourcePath,
        StoragePath? destinationPath,
        string reason)
    {
        if (sourcePath is null && destinationPath is null)
        {
            throw new ArgumentException("A skipped entry must have at least one path.");
        }

        return new(SynchronizationOperationKind.Skip, sourcePath, destinationPath, reason);
    }

    public static SynchronizationOperation Conflict(
        StoragePath sourcePath,
        StoragePath destinationPath,
        string reason)
    {
        ValidatePaths(sourcePath, destinationPath);
        return new(SynchronizationOperationKind.Conflict, sourcePath, destinationPath, reason);
    }

    /// <summary>
    /// Records a destination entry that a future deletion policy may remove.
    /// </summary>
    /// <remarks>
    /// A delete candidate is diagnostic plan data, not authorization to delete the entry.
    /// </remarks>
    public static SynchronizationOperation DeleteCandidate(
        StoragePath destinationPath,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(destinationPath);
        return new(SynchronizationOperationKind.DeleteCandidate, null, destinationPath, reason);
    }

    private static void ValidatePaths(StoragePath sourcePath, StoragePath destinationPath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPath);
    }
}
