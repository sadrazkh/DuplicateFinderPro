using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.Core.Services.Detectors;

/// <summary>
/// A pluggable strategy that groups a set of candidate files into duplicate
/// clusters. Each detector reports progress through the supplied callback.
/// </summary>
public interface IDuplicateDetector
{
    DetectionMethod Method { get; }

    Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileItem> files,
        ScanOptions options,
        IProgress<ScanProgress> progress,
        CancellationToken ct);
}
