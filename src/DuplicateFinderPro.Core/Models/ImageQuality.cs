namespace DuplicateFinderPro.Core.Models;

/// <summary>A quality issue flagged on a photo during the cleanup analysis.</summary>
public enum PhotoFlag
{
    Blurry,
    Dark,
    Overexposed,
    LowResolution,
    Screenshot,
}

/// <summary>
/// The result of analysing a single image for "is this photo worth keeping?" —
/// aimed at cleaning up phone galleries (blurry shots, dark frames, screenshots).
/// </summary>
public sealed class ImageQualityResult
{
    public required FileItem File { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Laplacian variance — higher means sharper, low means blurry.</summary>
    public double Sharpness { get; init; }

    /// <summary>Mean luminance 0..255.</summary>
    public double Brightness { get; init; }

    /// <summary>Overall 0..100 keep-worthiness score (higher = better).</summary>
    public int Score { get; init; }

    public List<PhotoFlag> Flags { get; init; } = new();

    public bool HasIssues => Flags.Count > 0;

    public long Length => File.Length;
    public string Megapixels => Width > 0 && Height > 0
        ? (Width * (double)Height / 1_000_000.0).ToString("0.0")
        : "-";
    public string Resolution => Width > 0 ? $"{Width}×{Height}" : "-";
}
