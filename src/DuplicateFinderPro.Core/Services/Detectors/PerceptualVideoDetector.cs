using System.Collections.Concurrent;
using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.Core.Services.Detectors;

/// <summary>
/// Finds the same video stored under different names, formats or qualities by
/// comparing per-frame perceptual signatures produced with ffmpeg. Two videos
/// match when a majority of their sampled frames align within the Hamming
/// threshold. Silently yields nothing when ffmpeg is unavailable.
/// </summary>
public sealed class PerceptualVideoDetector : IDuplicateDetector
{
    private const double FrameMatchFraction = 0.6;
    private readonly FfmpegVideoHasher _hasher;

    public DetectionMethod Method => DetectionMethod.PerceptualVideo;

    public bool IsAvailable => _hasher.IsAvailable;

    public PerceptualVideoDetector(FfmpegVideoHasher hasher) => _hasher = hasher;

    public async Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileItem> files,
        ScanOptions options,
        IProgress<ScanProgress> progress,
        CancellationToken ct)
    {
        if (!_hasher.IsAvailable)
            return Array.Empty<DuplicateGroup>();

        var videos = files.Where(f => MediaTypes.IsVideo(f.Extension)).ToList();
        if (videos.Count < 2)
            return Array.Empty<DuplicateGroup>();

        var signatures = new ConcurrentDictionary<FileItem, VideoSignature>();
        var dop = Math.Max(1, (options.MaxDegreeOfParallelism > 0 ? options.MaxDegreeOfParallelism : Environment.ProcessorCount) / 2);
        var processed = 0L;

        await Parallel.ForEachAsync(
            videos,
            new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = ct },
            async (file, token) =>
            {
                var sig = await _hasher.ComputeAsync(file.FullPath, options.VideoFrameSamples, token);
                if (!sig.IsEmpty) signatures[file] = sig;

                var done = Interlocked.Increment(ref processed);
                progress.Report(new ScanProgress
                {
                    Phase = ScanPhase.SamplingVideo,
                    StatusKey = "Status.SamplingVideo",
                    Total = videos.Count,
                    Processed = done,
                    CurrentFile = file.FullPath,
                });
            });

        var hashedVideos = signatures.Keys.ToList();
        if (hashedVideos.Count < 2)
            return Array.Empty<DuplicateGroup>();

        var uf = new UnionFind(hashedVideos.Count);
        for (var a = 0; a < hashedVideos.Count; a++)
        {
            ct.ThrowIfCancellationRequested();
            for (var b = a + 1; b < hashedVideos.Count; b++)
            {
                if (AreSimilar(signatures[hashedVideos[a]], signatures[hashedVideos[b]], options.PerceptualThreshold))
                    uf.Union(a, b);
            }
        }

        var groups = new List<DuplicateGroup>();
        foreach (var cluster in uf.Clusters())
        {
            if (cluster.Count < 2) continue;
            var members = cluster.Select(i => hashedVideos[i]).OrderByDescending(f => f.Length).ToList();
            groups.Add(new DuplicateGroup(Method, members, $"video:{members.Count}", FrameMatchFraction));
        }
        return groups;
    }

    /// <summary>
    /// Symmetric best-match: each frame of the shorter signature must find a
    /// counterpart within the Hamming threshold in the other, for at least
    /// <see cref="FrameMatchFraction"/> of frames.
    /// </summary>
    private static bool AreSimilar(VideoSignature x, VideoSignature y, int threshold)
    {
        var matches = 0;
        foreach (var hx in x.FrameHashes)
        {
            foreach (var hy in y.FrameHashes)
            {
                if (PerceptualHasher.HammingDistance(hx, hy) <= threshold)
                {
                    matches++;
                    break;
                }
            }
        }
        var denom = Math.Min(x.FrameHashes.Count, y.FrameHashes.Count);
        return denom > 0 && (double)matches / denom >= FrameMatchFraction;
    }
}
