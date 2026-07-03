namespace DuplicateFinderPro.Core.Models;

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
