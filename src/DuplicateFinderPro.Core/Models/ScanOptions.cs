namespace DuplicateFinderPro.Core.Models;

/// <summary>
/// User-configurable parameters that drive a scan.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>One or more root folders to scan recursively.</summary>
    public List<string> RootFolders { get; set; } = new();

    /// <summary>Combined set of detection strategies to run.</summary>
    public DetectionMethod Methods { get; set; } = DetectionMethod.ExactContent;

    /// <summary>Recurse into sub-directories.</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>Include files/folders flagged Hidden or System.</summary>
    public bool IncludeHidden { get; set; }

    /// <summary>Ignore files smaller than this many bytes (0 = no minimum).</summary>
    public long MinFileSizeBytes { get; set; }

    /// <summary>Ignore files larger than this many bytes (0 = no maximum).</summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>
    /// If non-empty, only extensions in this set are scanned (e.g. ".mkv", ".jpg").
    /// Stored lower-case, dot-prefixed.
    /// </summary>
    public HashSet<string> IncludeExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Extensions to always skip.</summary>
    public HashSet<string> ExcludeExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Absolute directory paths to exclude from the walk.</summary>
    public List<string> ExcludedFolders { get; set; } = new();

    /// <summary>Name-similarity threshold 0..1 (0.85 = 85% similar names match).</summary>
    public double NameSimilarityThreshold { get; set; } = 0.85;

    /// <summary>Max Hamming distance between perceptual hashes to be a match (0..64).</summary>
    public int PerceptualThreshold { get; set; } = 8;

    /// <summary>Number of frames sampled per video for perceptual video matching.</summary>
    public int VideoFrameSamples { get; set; } = 5;

    /// <summary>Optional explicit path to the ffmpeg executable.</summary>
    public string? FfmpegPath { get; set; }

    /// <summary>Degree of parallelism for hashing (0 = processor count).</summary>
    public int MaxDegreeOfParallelism { get; set; }
}
