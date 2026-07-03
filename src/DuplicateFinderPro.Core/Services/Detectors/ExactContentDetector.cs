using System.Collections.Concurrent;
using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.Core.Services.Detectors;

/// <summary>
/// Finds byte-for-byte identical files. Three-stage funnel:
///   1. group by exact size,
///   2. within each size group, split by a cheap head+tail quick hash,
///   3. within each quick-hash group, confirm with a full SHA-256.
/// Only files that survive all three stages are reported, so the result is exact.
/// </summary>
public sealed class ExactContentDetector : IDuplicateDetector
{
    public DetectionMethod Method => DetectionMethod.ExactContent;

    public async Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileItem> files,
        ScanOptions options,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        // Stage 1 — size buckets with more than one member.
        var bySize = files
            .GroupBy(f => f.Length)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        if (bySize.Count == 0)
            return Array.Empty<DuplicateGroup>();

        var dop = options.MaxDegreeOfParallelism > 0
            ? options.MaxDegreeOfParallelism
            : Environment.ProcessorCount;

        // Stage 2 — quick hash (size + head/tail sample).
        var quickGroups = await HashAsync(
            bySize, dop, ct, progress, ScanPhase.HashingContent,
            async f => $"{f.Length}:{await ContentHasher.QuickHashAsync(f.FullPath, f.Length, ct)}");

        var quickCandidates = quickGroups
            .Where(kv => kv.Value.Count > 1)
            .SelectMany(kv => kv.Value)
            .ToList();

        if (quickCandidates.Count == 0)
            return Array.Empty<DuplicateGroup>();

        // Stage 3 — full hash confirmation.
        var fullGroups = await HashAsync(
            quickCandidates, dop, ct, progress, ScanPhase.HashingContent,
            async f =>
            {
                f.ContentHash = await ContentHasher.FullHashAsync(f.FullPath, ct);
                return $"{f.Length}:{f.ContentHash}";
            });

        return fullGroups
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new DuplicateGroup(
                Method,
                kv.Value.OrderBy(f => f.LastWriteUtc).ToList(),
                kv.Key,
                similarity: 1.0))
            .ToList();
    }

    private static async Task<Dictionary<string, List<FileItem>>> HashAsync(
        IReadOnlyList<FileItem> items,
        int dop,
        CancellationToken ct,
        IProgress<ScanProgress> progress,
        ScanPhase phase,
        Func<FileItem, Task<string>> keySelector)
    {
        var result = new ConcurrentDictionary<string, List<FileItem>>();
        var processed = 0L;
        var total = items.Count;
        var failures = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = ct },
            async (file, token) =>
            {
                try
                {
                    var key = await keySelector(file);
                    var list = result.GetOrAdd(key, _ => new List<FileItem>());
                    lock (list) list.Add(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failures.Add(file.FullPath);
                }

                var done = Interlocked.Increment(ref processed);
                if (done % 16 == 0 || done == total)
                {
                    progress.Report(new ScanProgress
                    {
                        Phase = phase,
                        StatusKey = "Status.HashingContent",
                        Total = total,
                        Processed = done,
                        CurrentFile = file.FullPath,
                    });
                }
            });

        return new Dictionary<string, List<FileItem>>(result);
    }
}
