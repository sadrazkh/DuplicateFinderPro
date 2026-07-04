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

        // 4) Optional photo-quality pass (gallery cleanup).
        var photos = options.AnalyzeImageQuality
            ? await AnalyzePhotosAsync(files, options, progress, ct)
            : Array.Empty<ImageQualityResult>();

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
            Photos = photos,
            FileTypes = BuildFileTypeStats(files),
        };

        progress.Report(new ScanProgress
        {
            Phase = ScanPhase.Completed,
            StatusKey = "Status.Completed",
            GroupsFound = result.Groups.Count,
        });

        return result;
    }

    private static IReadOnlyList<FileTypeStat> BuildFileTypeStats(List<FileItem> files)
    {
        long imgC = 0, imgB = 0, vidC = 0, vidB = 0, othC = 0, othB = 0;
        foreach (var f in files)
        {
            if (Utils.MediaTypes.IsImage(f.Extension)) { imgC++; imgB += f.Length; }
            else if (Utils.MediaTypes.IsVideo(f.Extension)) { vidC++; vidB += f.Length; }
            else { othC++; othB += f.Length; }
        }
        return new List<FileTypeStat>
        {
            new("Images", (int)imgC, imgB),
            new("Videos", (int)vidC, vidB),
            new("Other", (int)othC, othB),
        };
    }

    private static async Task<IReadOnlyList<ImageQualityResult>> AnalyzePhotosAsync(
        List<FileItem> files, ScanOptions options, IProgress<ScanProgress> progress, CancellationToken ct)
    {
        var images = files.Where(f => Utils.MediaTypes.IsImage(f.Extension)).ToList();
        if (images.Count == 0) return Array.Empty<ImageQualityResult>();

        var dop = options.MaxDegreeOfParallelism > 0
            ? options.MaxDegreeOfParallelism
            : (options.GentleResourceUsage ? Math.Max(1, Environment.ProcessorCount / 2) : Environment.ProcessorCount);

        var results = new System.Collections.Concurrent.ConcurrentBag<ImageQualityResult>();
        var processed = 0L;

        await Parallel.ForEachAsync(
            images,
            new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = ct },
            async (file, token) =>
            {
                var r = await ImageQualityAnalyzer.AnalyzeAsync(file, options, token);
                if (r is not null) results.Add(r);

                var done = Interlocked.Increment(ref processed);
                if (done % 8 == 0 || done == images.Count)
                {
                    progress.Report(new ScanProgress
                    {
                        Phase = ScanPhase.AnalyzingPhotos,
                        StatusKey = "Status.AnalyzingPhotos",
                        Total = images.Count,
                        Processed = done,
                        CurrentFile = file.FullPath,
                    });
                }
            });

        return results.OrderBy(r => r.Score).ToList();
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
