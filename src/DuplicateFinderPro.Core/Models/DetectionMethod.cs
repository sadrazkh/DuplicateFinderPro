namespace DuplicateFinderPro.Core.Models;

/// <summary>
/// The strategy used to decide whether two files are considered duplicates.
/// </summary>
[Flags]
public enum DetectionMethod
{
    None = 0,

    /// <summary>Byte-for-byte identical content (size + SHA-256). Fast and exact.</summary>
    ExactContent = 1,

    /// <summary>Fuzzy file-name similarity (normalized Levenshtein ratio).</summary>
    NameSimilarity = 2,

    /// <summary>Perceptual image similarity (dHash / Hamming distance).</summary>
    PerceptualImage = 4,

    /// <summary>Perceptual video similarity via sampled frames (requires ffmpeg).</summary>
    PerceptualVideo = 8,
}
