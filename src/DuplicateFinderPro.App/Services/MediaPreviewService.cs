using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DuplicateFinderPro.Core.Services;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.App.Services;

/// <summary>
/// Produces preview thumbnails for files: images are decoded directly; videos
/// have a middle frame extracted with ffmpeg (cached to a temp folder). Results
/// are memoised so scrolling a list doesn't re-decode. All bitmaps are frozen so
/// they can be created off the UI thread.
/// </summary>
public sealed class MediaPreviewService
{
    public static MediaPreviewService Instance { get; } = new();

    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "DuplicateFinderPro", "thumbs");
    private readonly ConcurrentDictionary<string, ImageSource> _memCache = new();
    private FfmpegVideoHasher? _hasher;
    private string? _hasherPath;

    /// <summary>Path to ffmpeg used for video frames (null = auto/PATH).</summary>
    public string? FfmpegPath { get; set; }

    public bool Gentle { get; set; } = true;

    public async Task<ImageSource?> GetThumbnailAsync(string fullPath, string extension, int decodeWidth, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;

        var key = $"{fullPath}|{decodeWidth}";
        if (_memCache.TryGetValue(key, out var cached)) return cached;

        ImageSource? source = null;
        try
        {
            if (MediaTypes.IsImage(extension))
                source = await Task.Run(() => LoadBitmap(fullPath, decodeWidth), ct);
            else if (MediaTypes.IsVideo(extension))
                source = await LoadVideoFrameAsync(fullPath, decodeWidth, ct);
        }
        catch
        {
            source = null;
        }

        if (source is not null) _memCache[key] = source;
        return source;
    }

    private async Task<ImageSource?> LoadVideoFrameAsync(string videoPath, int decodeWidth, CancellationToken ct)
    {
        var hasher = GetHasher();
        if (!hasher.IsAvailable) return null;

        Directory.CreateDirectory(_cacheDir);
        var cacheFile = Path.Combine(_cacheDir, CacheKey(videoPath) + ".jpg");

        if (!File.Exists(cacheFile))
        {
            var ok = await hasher.ExtractThumbnailAsync(videoPath, cacheFile, Gentle, ct);
            if (!ok) return null;
        }

        return await Task.Run(() => LoadBitmap(cacheFile, decodeWidth), ct);
    }

    private FfmpegVideoHasher GetHasher()
    {
        if (_hasher is null || _hasherPath != FfmpegPath)
        {
            _hasher = new FfmpegVideoHasher(FfmpegPath);
            _hasherPath = FfmpegPath;
        }
        return _hasher;
    }

    private static ImageSource? LoadBitmap(string path, int decodeWidth)
    {
        if (!File.Exists(path)) return null;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bmp.DecodePixelWidth = decodeWidth;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private string CacheKey(string path)
    {
        long len = 0; long ticks = 0;
        try { var fi = new FileInfo(path); len = fi.Length; ticks = fi.LastWriteTimeUtc.Ticks; } catch { }
        var bytes = Encoding.UTF8.GetBytes($"{path}|{len}|{ticks}");
        return Convert.ToHexStringLower(SHA1.HashData(bytes));
    }
}
