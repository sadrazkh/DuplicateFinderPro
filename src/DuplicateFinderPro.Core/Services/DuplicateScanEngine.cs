using System.Diagnostics;
using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Services.Detectors;

namespace DuplicateFinderPro.Core.Services;

/// <summary>
/// Top-level orchestrator: enumerates files once, then runs each requested
/// detector over the shared candidate set, aggregating groups, progress and
/// warnings into a single <see cref="ScanResult"/>.
/// </summary>
public sealed class DuplicateScanEngine
{
    public async Task<ScanResult> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var warnings = new List<string>();

        // 1) Enumerate the file universe once.
        progress.Report(new ScanProgress { Phase = ScanPhase.Enumerating, StatusKey = "Status.Enumerating" });
        var scanner = new FileScanner();
        var files = new List<FileItem>();
        foreach (var item in scanner.Enumerate(options, ct))
        {
            files.Add(item);
            if (files.Count % 200 == 0)
            {
                progress.Report(new ScanProgress
                {
                    Phase = ScanPhase.Enumerating,
                    StatusKey = "Status.Enumerating",
                    Processed = files.Count,
                    CurrentFile = item.FullPath,
                });
            }
        }
        warnings.AddRange(scanner.Warnings);

        // 2) Build the requested detector pipeline.
        var detectors = BuildDetectors(options, warnings);

        // 3) Run each detector, collecting groups.
        var allGroups = new List<DuplicateGroup>();
        foreach (var detector in detectors)
        {
            ct.ThrowIfCancellationRequested();
            var groups = await detector.DetectAsync(files, options, progress, ct);
            allGroups.AddRange(groups);
        }

        progress.Report(new ScanProgress
        {
            Phase = ScanPhase.Finalizing,
            StatusKey = "Status.Finalizing",
            GroupsFound = allGroups.Count,
        });

        sw.Stop();

        var result = new ScanResult
        {
            Groups = allGroups
                .OrderByDescending(g => g.ReclaimableBytes)
                .ThenByDescending(g => g.Count)
                .ToList(),
            FilesScanned = files.Count,
            BytesScanned = files.Sum(f => f.Length),
            Elapsed = sw.Elapsed,
            Warnings = warnings,
        };

        progress.Report(new ScanProgress
        {
            Phase = ScanPhase.Completed,
            StatusKey = "Status.Completed",
            GroupsFound = result.Groups.Count,
        });

        return result;
    }

    private static List<IDuplicateDetector> BuildDetectors(ScanOptions options, List<string> warnings)
    {
        var detectors = new List<IDuplicateDetector>();

        if (options.Methods.HasFlag(DetectionMethod.ExactContent))
            detectors.Add(new ExactContentDetector());

        if (options.Methods.HasFlag(DetectionMethod.NameSimilarity))
            detectors.Add(new NameSimilarityDetector());

        if (options.Methods.HasFlag(DetectionMethod.PerceptualImage))
            detectors.Add(new PerceptualImageDetector());

        if (options.Methods.HasFlag(DetectionMethod.PerceptualVideo))
        {
            var hasher = new FfmpegVideoHasher(options.FfmpegPath);
            if (hasher.IsAvailable)
                detectors.Add(new PerceptualVideoDetector(hasher));
            else
                warnings.Add("ffmpeg not found — perceptual video matching skipped.");
        }

        return detectors;
    }
}
