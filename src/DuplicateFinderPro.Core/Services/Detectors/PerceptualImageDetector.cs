using System.Collections.Concurrent;
using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.Core.Services.Detectors;

/// <summary>
/// Finds visually-identical images regardless of format, resolution or
/// re-compression by comparing 64-bit dHash fingerprints within a Hamming
/// distance threshold. Clustering uses union-find over near matches.
/// </summary>
public sealed class PerceptualImageDetector : IDuplicateDetector
{
    public DetectionMethod Method => DetectionMethod.PerceptualImage;

    public async Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileItem> files,
        ScanOptions options,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        var images = files.Where(f => MediaTypes.IsImage(f.Extension)).ToList();
        if (images.Count < 2)
            return Array.Empty<DuplicateGroup>();

        var dop = options.MaxDegreeOfParallelism > 0
            ? options.MaxDegreeOfParallelism
            : (options.GentleResourceUsage ? Math.Max(1, Environment.ProcessorCount / 2) : Environment.ProcessorCount);
        var processed = 0L;

        await Parallel.ForEachAsync(
            images,
            new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = ct },
            async (file, token) =>
            {
                file.PerceptualHash = await PerceptualHasher.ComputeAsync(file.FullPath, token);
                var done = Interlocked.Increment(ref processed);
                if (done % 8 == 0 || done == images.Count)
                {
                    progress.Report(new ScanProgress
                    {
                        Phase = ScanPhase.HashingPerceptual,
                        StatusKey = "Status.HashingPerceptual",
                        Total = images.Count,
                        Processed = done,
                        CurrentFile = file.FullPath,
                    });
                }
            });

        var hashed = images.Where(f => f.PerceptualHash is not null).ToList();
        return Cluster(hashed, options.PerceptualThreshold, Method, ct);
    }

    /// <summary>Union-find clustering over pairwise Hamming distance ≤ threshold.</summary>
    internal static List<DuplicateGroup> Cluster(List<FileItem> hashed, int threshold, DetectionMethod method, CancellationToken ct)
    {
        var uf = new UnionFind(hashed.Count);
        for (var a = 0; a < hashed.Count; a++)
        {
            ct.ThrowIfCancellationRequested();
            for (var b = a + 1; b < hashed.Count; b++)
            {
                var dist = PerceptualHasher.HammingDistance(hashed[a].PerceptualHash!.Value, hashed[b].PerceptualHash!.Value);
                if (dist <= threshold)
                    uf.Union(a, b);
            }
        }

        var groups = new List<DuplicateGroup>();
        foreach (var cluster in uf.Clusters())
        {
            if (cluster.Count < 2) continue;
            var members = cluster.Select(i => hashed[i]).OrderByDescending(f => f.Length).ToList();

            // Real similarity: average Hamming closeness to the representative hash.
            var rep = members[0].PerceptualHash!.Value;
            var avgDistance = members.Skip(1)
                .Select(m => (double)PerceptualHasher.HammingDistance(rep, m.PerceptualHash!.Value))
                .DefaultIfEmpty(0)
                .Average();
            var similarity = 1.0 - avgDistance / 64.0;

            groups.Add(new DuplicateGroup(
                method,
                members,
                members[0].PerceptualHash!.Value.ToString("x16"),
                Math.Clamp(similarity, 0, 1)));
        }
        return groups;
    }
}
