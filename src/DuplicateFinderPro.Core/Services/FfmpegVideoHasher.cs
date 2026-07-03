using System.Diagnostics;
using System.Globalization;
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
/// Requires ffmpeg (and ffprobe) on PATH or an explicit path in ScanOptions.
/// </summary>
public sealed class FfmpegVideoHasher
{
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

    /// <summary>Samples <paramref name="frameCount"/> frames and hashes each.</summary>
    public async Task<VideoSignature> ComputeAsync(string path, int frameCount, CancellationToken ct)
    {
        if (!IsAvailable) return new VideoSignature(Array.Empty<ulong>());

        var duration = await ProbeDurationSecondsAsync(path, ct);
        var hashes = new List<ulong>();

        // Sample at interior points to skip intros/black frames at the very edges.
        for (var i = 1; i <= frameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var position = duration > 0
                ? duration * i / (frameCount + 1)
                : i; // Unknown duration: fall back to fixed seconds.

            var frame = await ExtractFrameAsync(path, position, ct);
            if (frame is null) continue;

            using var ms = new MemoryStream(frame);
            var hash = await PerceptualHasher.ComputeAsync(ms, ct);
            if (hash is not null) hashes.Add(hash.Value);
        }

        return new VideoSignature(hashes);
    }

    private async Task<byte[]?> ExtractFrameAsync(string path, double seconds, CancellationToken ct)
    {
        var args = string.Create(CultureInfo.InvariantCulture,
            $"-hide_banner -loglevel error -ss {seconds:0.###} -i \"{path}\" -frames:v 1 -f image2pipe -vcodec png pipe:1");

        using var proc = StartProcess(_ffmpeg, args, redirectStdout: true);
        if (proc is null) return null;

        using var buffer = new MemoryStream();
        var copyTask = proc.StandardOutput.BaseStream.CopyToAsync(buffer, ct);
        try
        {
            await proc.WaitForExitAsync(ct);
            await copyTask;
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
        // Probe PATH by attempting a version call.
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
