using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.Core.Services.Detectors;

/// <summary>
/// Clusters files whose names describe the same thing even when spelled
/// differently ("Movie.mkv" vs "Movie - Copy (1).mkv"). Names are normalized,
/// then compared with a normalized Levenshtein ratio; matches above the
/// configured threshold are unioned into clusters.
/// </summary>
public sealed class NameSimilarityDetector : IDuplicateDetector
{
    public DetectionMethod Method => DetectionMethod.NameSimilarity;

    public Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileItem> files,
        ScanOptions options,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        foreach (var f in files)
            f.NormalizedName = StringSimilarity.Normalize(Path.GetFileNameWithoutExtension(f.FileName));

        // Group by first character bucket to keep the pairwise comparison
        // tractable; near-identical names almost always share a first letter.
        var candidates = files.Where(f => !string.IsNullOrWhiteSpace(f.NormalizedName)).ToList();
        var uf = new UnionFind(candidates.Count);
        var threshold = options.NameSimilarityThreshold;

        var buckets = candidates
            .Select((f, i) => (f, i))
            .GroupBy(t => t.f.NormalizedName![0]);

        long processed = 0;
        long total = candidates.Count;

        foreach (var bucket in buckets)
        {
            ct.ThrowIfCancellationRequested();
            var arr = bucket.ToList();
            for (var a = 0; a < arr.Count; a++)
            {
                for (var b = a + 1; b < arr.Count; b++)
                {
                    var ratio = StringSimilarity.Ratio(arr[a].f.NormalizedName!, arr[b].f.NormalizedName!);
                    if (ratio >= threshold)
                        uf.Union(arr[a].i, arr[b].i);
                }

                if (++processed % 64 == 0)
                {
                    progress.Report(new ScanProgress
                    {
                        Phase = ScanPhase.MatchingNames,
                        StatusKey = "Status.MatchingNames",
                        Total = total,
                        Processed = processed,
                        CurrentFile = arr[a].f.FullPath,
                    });
                }
            }
        }

        var groups = new List<DuplicateGroup>();
        foreach (var cluster in uf.Clusters())
        {
            if (cluster.Count < 2) continue;
            var members = cluster.Select(i => candidates[i]).OrderBy(f => f.FileName).ToList();
            groups.Add(new DuplicateGroup(
                Method,
                members,
                members[0].NormalizedName ?? members[0].FileName,
                similarity: threshold));
        }

        return Task.FromResult<IReadOnlyList<DuplicateGroup>>(groups);
    }
}
