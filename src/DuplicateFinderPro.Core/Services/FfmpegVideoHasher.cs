using System.Diagnostics;
using System.Globalization;
using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.Core.Services;

/// <summary>A perceptual fingerprint for a video: dHashes of evenly-sampled frames.</summary>
public sealed record VideoSignature(IReadOnlyList<ulong> FrameHashes)
{
    public bool IsEmpty => FrameHashes.Count == 0;
}

/// <summary>
/// Extracts frames with ffmpeg and reduces each to a perceptual hash, giving a
/// signature that survives re-encoding, resolution and container changes — so
/// the same movie stored twice in different qualities/formats can be matched.
///
/// Design goals:
///  • Sample frames spread across the *whole* runtime (intros are skipped) so
///    films that share an opening logo aren't falsely merged.
///  • Extract every frame in a *single* ffmpeg pass (fps filter) instead of one
///    process per frame — far fewer spawns and disk seeks.
///  • Stay gentle: single-threaded ffmpeg at below-normal priority, downscaled
///    frames, so a large library scan never saturates the machine.
///
/// Requires ffmpeg (and ffprobe) on PATH or an explicit path in ScanOptions.
/// </summary>
public sealed class FfmpegVideoHasher
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly string _ffmpeg;
    private readonly string _ffprobe;

    public bool IsAvailable { get; }

    public FfmpegVideoHasher(string? explicitFfmpegPath = null)
    {
        _ffmpeg = ResolveTool(explicitFfmpegPath, "ffmpeg");
        _ffprobe = ResolveTool(
            explicitFfmpegPath is null ? null : Path.Combine(Path.GetDirectoryName(explicitFfmpegPath) ?? "", ProbeName(explicitFfmpegPath)),
            "ffprobe");
        IsAvailable = ToolExists(_ffmpeg);
    }

    /// <summary>
    /// Samples frames from random positions spread across the whole runtime and
    /// hashes each. Positions are one-per-segment random picks (see
    /// <see cref="ScanOptions.VideoSampleSeed"/>) so the check covers the entire
    /// film — beginning, middle and end — not just its edges.
    /// </summary>
    public async Task<VideoSignature> ComputeAsync(string path, ScanOptions options, CancellationToken ct)
    {
        if (!IsAvailable) return new VideoSignature(Array.Empty<ulong>());

        var frameCount = Math.Clamp(options.VideoFrameSamples, 2, 60);
        var duration = await ProbeDurationSecondsAsync(path, ct);

        // Unknown duration: fall back to a single low-fps pass across the file.
        if (duration <= 0)
        {
            var payload = await ExtractFallbackFramesAsync(path, frameCount, options, ct);
            return new VideoSignature(HashPngStream(payload, frameCount, ct));
        }

        var positions = BuildSamplePositions(duration, frameCount, options);
        var hashes = new List<ulong>(positions.Count);
        foreach (var seconds in positions)
        {
            ct.ThrowIfCancellationRequested();
            var png = await ExtractFrameAsync(path, seconds, options.GentleResourceUsage, ct);
            if (png is null) continue;
            using var ms = new MemoryStream(png);
            var hash = await PerceptualHasher.ComputeAsync(ms, ct);
            if (hash is not null) hashes.Add(hash.Value);
        }

        return new VideoSignature(hashes);
    }

    /// <summary>
    /// Divides [intro%, 100-outro%] into <paramref name="frameCount"/> equal
    /// segments and picks one random timestamp inside each. With a fixed seed
    /// (shared across the scan) two copies of the same film get identical
    /// positions and therefore still match, while a different run probes
    /// different spots.
    /// </summary>
    private static List<double> BuildSamplePositions(double duration, int frameCount, ScanOptions options)
    {
        var intro = Math.Clamp(options.VideoIntroSkipPercent, 0, 40) / 100.0;
        var outro = Math.Clamp(options.VideoOutroSkipPercent, 0, 40) / 100.0;

        var start = duration * intro;
        var window = Math.Max(duration * (1 - intro - outro), 0.5);
        var segment = window / frameCount;

        var rng = options.VideoSampleSeed != 0 ? new Random(options.VideoSampleSeed) : null;
        var positions = new List<double>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            // Random point within the segment, or its midpoint when seed = 0.
            var offset = rng is not null ? rng.NextDouble() : 0.5;
            positions.Add(start + segment * (i + offset));
        }
        return positions;
    }

    /// <summary>
    /// Writes a single JPEG frame taken from the middle of the video to
    /// <paramref name="outputImagePath"/> (for a preview thumbnail). Returns
    /// true on success. No-op if ffmpeg is unavailable.
    /// </summary>
    public async Task<bool> ExtractThumbnailAsync(string videoPath, string outputImagePath, bool gentle, CancellationToken ct)
    {
        if (!IsAvailable) return false;

        var duration = await ProbeDurationSecondsAsync(videoPath, ct);
        var position = duration > 0 ? duration * 0.5 : 5;
        var threads = gentle ? "-threads 1 " : string.Empty;

        var args = string.Create(CultureInfo.InvariantCulture,
            $"-hide_banner -loglevel error -y -ss {position:0.###} {threads}-i \"{videoPath}\" " +
            $"-frames:v 1 -vf scale=640:-2 -q:v 4 \"{outputImagePath}\"");

        using var proc = StartProcess(_ffmpeg, args, redirectStdout: false);
        if (proc is null) return false;
        if (gentle) TrySetPriority(proc, ProcessPriorityClass.BelowNormal);

        var drainErr = proc.StandardError.ReadToEndAsync(ct);
        try
        {
            await proc.WaitForExitAsync(ct);
            await drainErr;
        }
        catch (OperationCanceledException)
        {
            TryKill(proc);
            throw;
        }

        return File.Exists(outputImagePath) && new FileInfo(outputImagePath).Length > 0;
    }

    /// <summary>Extracts a single downscaled PNG frame at the given timestamp.</summary>
    private async Task<byte[]?> ExtractFrameAsync(string path, double seconds, bool gentle, CancellationToken ct)
    {
        var threads = gentle ? "-threads 1 " : string.Empty;
        var args = string.Create(CultureInfo.InvariantCulture,
            $"-hide_banner -loglevel error -ss {seconds:0.###} {threads}-i \"{path}\" -an " +
            $"-frames:v 1 -vf scale=160:-2 -f image2pipe -vcodec png pipe:1");
        return await RunToBytesAsync(_ffmpeg, args, gentle, ct);
    }

    /// <summary>Duration unknown: grab a bounded number of frames at a low fps.</summary>
    private async Task<byte[]?> ExtractFallbackFramesAsync(string path, int frameCount, ScanOptions options, CancellationToken ct)
    {
        var threads = options.GentleResourceUsage ? "-threads 1 " : string.Empty;
        var args = string.Create(CultureInfo.InvariantCulture,
            $"-hide_banner -loglevel error {threads}-i \"{path}\" -an " +
            $"-vf fps=1/10,scale=160:-2 -frames:v {frameCount} -f image2pipe -vcodec png pipe:1");
        return await RunToBytesAsync(_ffmpeg, args, options.GentleResourceUsage, ct);
    }

    private static List<ulong> HashPngStream(byte[]? payload, int frameCount, CancellationToken ct)
    {
        var hashes = new List<ulong>(frameCount);
        if (payload is null || payload.Length == 0) return hashes;
        foreach (var png in SplitPngFrames(payload))
        {
            ct.ThrowIfCancellationRequested();
            using var ms = new MemoryStream(png);
            var hash = PerceptualHasher.ComputeAsync(ms, ct).GetAwaiter().GetResult();
            if (hash is not null) hashes.Add(hash.Value);
        }
        return hashes;
    }

    private async Task<byte[]?> RunToBytesAsync(string fileName, string arguments, bool gentle, CancellationToken ct)
    {
        using var proc = StartProcess(fileName, arguments, redirectStdout: true);
        if (proc is null) return null;

        if (gentle) TrySetPriority(proc, ProcessPriorityClass.BelowNormal);

        using var buffer = new MemoryStream();
        var copyTask = proc.StandardOutput.BaseStream.CopyToAsync(buffer, ct);
        var drainErr = proc.StandardError.ReadToEndAsync(ct); // avoid stderr deadlock
        try
        {
            await Task.WhenAll(copyTask, drainErr);
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(proc);
            throw;
        }

        return buffer.Length > 0 ? buffer.ToArray() : null;
    }

    private async Task<double> ProbeDurationSecondsAsync(string path, CancellationToken ct)
    {
        if (!ToolExists(_ffprobe)) return 0;

        var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{path}\"";
        using var proc = StartProcess(_ffprobe, args, redirectStdout: true);
        if (proc is null) return 0;

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    /// <summary>Splits a stream of concatenated PNGs (from image2pipe) into frames.</summary>
    private static IEnumerable<byte[]> SplitPngFrames(byte[] data)
    {
        var starts = new List<int>();
        for (var i = 0; i + PngSignature.Length <= data.Length; i++)
        {
            if (MatchesAt(data, i))
                starts.Add(i);
        }

        for (var s = 0; s < starts.Count; s++)
        {
            var begin = starts[s];
            var end = s + 1 < starts.Count ? starts[s + 1] : data.Length;
            var frame = new byte[end - begin];
            Array.Copy(data, begin, frame, 0, frame.Length);
            yield return frame;
        }
    }

    private static bool MatchesAt(byte[] data, int index)
    {
        for (var k = 0; k < PngSignature.Length; k++)
            if (data[index + k] != PngSignature[k])
                return false;
        return true;
    }

    private static Process? StartProcess(string fileName, string arguments, bool redirectStdout)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = redirectStdout,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }

    private static void TrySetPriority(Process proc, ProcessPriorityClass priority)
    {
        try { if (!proc.HasExited) proc.PriorityClass = priority; } catch { /* raced with exit */ }
    }

    private static void TryKill(Process proc)
    {
        try { if (!proc.HasExited) proc.Kill(true); } catch { /* best effort */ }
    }

    private static string ResolveTool(string? explicitPath, string toolName)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;
        return OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
    }

    private static string ProbeName(string ffmpegPath)
    {
        var name = Path.GetFileNameWithoutExtension(ffmpegPath).Replace("ffmpeg", "ffprobe");
        var ext = Path.GetExtension(ffmpegPath);
        return name + ext;
    }

    private static bool ToolExists(string tool)
    {
        if (File.Exists(tool)) return true;
        try
        {
            using var proc = StartProcess(tool, "-version", redirectStdout: true);
            if (proc is null) return false;
            proc.WaitForExit(3000);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
