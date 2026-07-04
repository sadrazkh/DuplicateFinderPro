namespace DuplicateFinderPro.Core.Models;

/// <summary>Aggregate count and size of one file category (Images/Videos/Other).</summary>
public sealed record FileTypeStat(string Category, int Count, long Bytes);

/// <summary>
/// The full outcome of a completed scan.
/// </summary>
public sealed class ScanResult
{
    public required IReadOnlyList<DuplicateGroup> Groups { get; init; }
    public required int FilesScanned { get; init; }
    public required long BytesScanned { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Per-image quality assessment (empty unless photo analysis was requested).</summary>
    public IReadOnlyList<ImageQualityResult> Photos { get; init; } = Array.Empty<ImageQualityResult>();

    /// <summary>Count/size of scanned files grouped into Images / Videos / Other.</summary>
    public IReadOnlyList<FileTypeStat> FileTypes { get; init; } = Array.Empty<FileTypeStat>();

    public int DuplicateFileCount => Groups.Sum(g => g.Count);

    public int RedundantFileCount => Groups.Sum(g => g.Count - 1);

    public long ReclaimableBytes => Groups.Sum(g => g.ReclaimableBytes);

    public static ScanResult Empty { get; } = new()
    {
        Groups = Array.Empty<DuplicateGroup>(),
        FilesScanned = 0,
        BytesScanned = 0,
        Elapsed = TimeSpan.Zero,
    };
}
