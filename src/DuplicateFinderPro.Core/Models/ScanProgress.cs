namespace DuplicateFinderPro.Core.Models;

/// <summary>The coarse phase the engine is currently executing.</summary>
public enum ScanPhase
{
    Idle,
    Enumerating,
    QuickHashing,
    HashingContent,
    MatchingNames,
    HashingPerceptual,
    SamplingVideo,
    AnalyzingPhotos,
    Finalizing,
    Completed,
    Cancelled,
    Faulted,
}

/// <summary>
/// Immutable progress snapshot pushed to the UI while a scan runs.
/// </summary>
public sealed record ScanProgress
{
    public ScanPhase Phase { get; init; }

    /// <summary>Localization key describing the current phase/status.</summary>
    public string StatusKey { get; init; } = string.Empty;

    /// <summary>Total items in the current phase (0 = indeterminate).</summary>
    public long Total { get; init; }

    /// <summary>Items processed in the current phase.</summary>
    public long Processed { get; init; }

    /// <summary>File currently being processed, if any.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>Groups discovered so far.</summary>
    public int GroupsFound { get; init; }

    /// <summary>Fraction (0..1) of the file currently being hashed (big files).</summary>
    public double CurrentFileFraction { get; init; }

    /// <summary>True for lightweight per-file byte-progress pings (no count update).</summary>
    public bool IsFileTick { get; init; }

    public double Percentage => Total <= 0 ? 0 : Math.Min(100.0, Processed * 100.0 / Total);
}
