using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace DuplicateFinderPro.App.Services;

/// <summary>
/// Downloads a self-contained Windows ffmpeg build (ffmpeg.exe + ffprobe.exe)
/// into the app's data folder so the perceptual-video method works without the
/// user installing anything. The download is user-initiated from Settings.
/// </summary>
public sealed class FfmpegInstaller
{
    // BtbN publishes a stable "latest" redirect that always points at a current build.
    private const string DownloadUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip";

    private static readonly HttpClient Http = CreateClient();

    public static string InstallDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DuplicateFinderPro", "ffmpeg");

    public string FfmpegExePath => Path.Combine(InstallDir, "ffmpeg.exe");
    public string FfprobeExePath => Path.Combine(InstallDir, "ffprobe.exe");

    /// <summary>True once both binaries are present in the app data folder.</summary>
    public bool IsInstalled => File.Exists(FfmpegExePath) && File.Exists(FfprobeExePath);

    /// <summary>Returns the managed ffmpeg path if it has been downloaded, else null.</summary>
    public string? LocateInstalled() => IsInstalled ? FfmpegExePath : null;

    /// <summary>
    /// Downloads and extracts ffmpeg/ffprobe. <paramref name="progress"/> reports 0..100.
    /// Returns the path to ffmpeg.exe on success.
    /// </summary>
    public async Task<string> InstallAsync(IProgress<double> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(InstallDir);
        var tempZip = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.zip");

        try
        {
            await DownloadAsync(tempZip, progress, ct);
            ExtractBinaries(tempZip);
            progress.Report(100);

            if (!IsInstalled)
                throw new InvalidOperationException("ffmpeg/ffprobe not found inside the downloaded archive.");

            return FfmpegExePath;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* temp cleanup */ }
        }
    }

    private static async Task DownloadAsync(string destination, IProgress<double> progress, CancellationToken ct)
    {
        using var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total > 0)
                progress.Report(readTotal * 90.0 / total); // download = first 90%
        }
    }

    /// <summary>Pulls only ffmpeg.exe and ffprobe.exe out of the archive, flattening the path.</summary>
    private void ExtractBinaries(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                entry.ExtractToFile(FfmpegExePath, overwrite: true);
            else if (name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                entry.ExtractToFile(FfprobeExePath, overwrite: true);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DuplicateFinderPro/1.0");
        return client;
    }
}
