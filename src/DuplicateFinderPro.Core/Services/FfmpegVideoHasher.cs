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

    /// <summary>Samples frames spread across the runtime and hashes each.</summary>
    public async Task<VideoSignature> ComputeAsync(string path, ScanOptions options, CancellationToken ct)
    {
        if (!IsAvailable) return new VideoSignature(Array.Empty<ulong>());

        var frameCount = Math.Clamp(options.VideoFrameSamples, 2, 60);
        var duration = await ProbeDurationSecondsAsync(path, ct);

        byte[]? payload = duration > 0
            ? await ExtractSpreadFramesAsync(path, duration, frameCount, options, ct)
            : await ExtractFallbackFramesAsync(path, frameCount, options, ct);

        if (payload is null || payload.Length == 0)
            return new VideoSignature(Array.Empty<ulong>());

        var hashes = new List<ulong>(frameCount);
        foreach (var png in SplitPngFrames(payload))
        {
            ct.ThrowIfCancellationRequested();
            using var ms = new MemoryStream(png);
            var hash = await PerceptualHasher.ComputeAsync(ms, ct);
            if (hash is not null) hashes.Add(hash.Value);
        }

        return new VideoSignature(hashes);
    }

    /// <summary>
    /// One ffmpeg pass across [intro%, 100-outro%] of the runtime, emitting
    /// exactly <paramref name="frameCount"/> evenly-spaced, downscaled PNGs.
    /// </summary>
    private async Task<byte[]?> ExtractSpreadFramesAsync(string path, double duration, int frameCount, ScanOptions options, CancellationToken ct)
    {
        var intro = Math.Clamp(options.VideoIntroSkipPercent, 0, 40) / 100.0;
        var outro = Math.Clamp(options.VideoOutroSkipPercent, 0, 40) / 100.0;

        var start = duration * intro;
        var window = Math.Max(duration * (1 - intro - outro), 0.5);
        var fps = frameCount / window; // frames per second that yields ~frameCount frames

        var threads = options.GentleResourceUsage ? "-threads 1 " : string.Empty;
        var args = string.Create(CultureInfo.InvariantCulture,
            $"-hide_banner -loglevel error -ss {start:0.###} {threads}-i \"{path}\" -t {window:0.###} -an " +
            $"-vf fps={fps:0.######},scale=160:-2 -frames:v {frameCount} -f image2pipe -vcodec png pipe:1");

        return await RunToBytesAsync(_ffmpeg, args, options.GentleResourceUsage, ct);
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
