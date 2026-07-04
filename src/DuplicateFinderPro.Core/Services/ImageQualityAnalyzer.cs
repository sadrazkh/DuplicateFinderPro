using DuplicateFinderPro.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DuplicateFinderPro.Core.Services;

/// <summary>
/// Heuristic "is this photo worth keeping?" analyser. Measures sharpness
/// (variance of the Laplacian), brightness and resolution, and recognises
/// screenshots — the things that make phone-gallery cleanup tedious by hand.
/// Everything is a heuristic, so results are advisory, not authoritative.
/// </summary>
public static class ImageQualityAnalyzer
{
    // Analyse on a small grayscale copy: fast and resolution-independent.
    private const int WorkingSize = 320;

    // Common screen/phone resolutions (either orientation) → likely a screenshot.
    private static readonly HashSet<(int, int)> ScreenResolutions = new()
    {
        (1080, 1920), (1080, 2340), (1080, 2400), (1080, 2280), (1080, 2160),
        (1170, 2532), (1284, 2778), (1179, 2556), (1290, 2796),
        (720, 1280), (750, 1334), (828, 1792), (1125, 2436),
        (1440, 2560), (1440, 2960), (1440, 3040), (1440, 3200),
        (1920, 1080), (2560, 1440), (3840, 2160), (1366, 768), (1536, 864),
        (1280, 720), (1600, 900), (2880, 1800), (2560, 1600),
    };

    public static async Task<ImageQualityResult?> AnalyzeAsync(FileItem file, ScanOptions options, CancellationToken ct)
    {
        try
        {
            await using var fs = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            using var image = await Image.LoadAsync<L8>(fs, ct);

            var width = image.Width;
            var height = image.Height;

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(WorkingSize, WorkingSize),
                Mode = ResizeMode.Max,
            }));

            var (sharpness, brightness) = Measure(image);
            var flags = Classify(file, width, height, sharpness, brightness, options);
            var score = ComputeScore(sharpness, brightness, width, height, flags);

            return new ImageQualityResult
            {
                File = file,
                Width = width,
                Height = height,
                Sharpness = sharpness,
                Brightness = brightness,
                Score = score,
                Flags = flags,
            };
        }
        catch
        {
            return null; // unreadable/corrupt — skip
        }
    }

    /// <summary>Returns (Laplacian variance, mean brightness) over a grayscale image.</summary>
    private static (double sharpness, double brightness) Measure(Image<L8> image)
    {
        var w = image.Width;
        var h = image.Height;
        var gray = new byte[w * h];

        double sum = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                {
                    var v = row[x].PackedValue;
                    gray[y * w + x] = v;
                    sum += v;
                }
            }
        });

        var brightness = sum / (w * h);

        // Discrete Laplacian (4-neighbour) variance over interior pixels.
        double lapSum = 0, lapSqSum = 0;
        long count = 0;
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                var i = y * w + x;
                int lap = 4 * gray[i] - gray[i - 1] - gray[i + 1] - gray[i - w] - gray[i + w];
                lapSum += lap;
                lapSqSum += (double)lap * lap;
                count++;
            }
        }

        if (count == 0) return (0, brightness);
        var mean = lapSum / count;
        var variance = lapSqSum / count - mean * mean;
        return (Math.Max(0, variance), brightness);
    }

    private static List<PhotoFlag> Classify(FileItem file, int width, int height, double sharpness, double brightness, ScanOptions options)
    {
        var flags = new List<PhotoFlag>();

        if (sharpness < options.BlurThreshold) flags.Add(PhotoFlag.Blurry);
        if (brightness < options.DarkThreshold) flags.Add(PhotoFlag.Dark);
        if (brightness > 238) flags.Add(PhotoFlag.Overexposed);
        if (Math.Max(width, height) < options.LowResolutionThreshold) flags.Add(PhotoFlag.LowResolution);
        if (IsScreenshot(file, width, height)) flags.Add(PhotoFlag.Screenshot);

        return flags;
    }

    private static bool IsScreenshot(FileItem file, int width, int height)
    {
        var name = file.FileName.ToLowerInvariant();
        if (name.Contains("screenshot") || name.Contains("screen shot") ||
            name.Contains("screen_") || name.StartsWith("scr_") || name.Contains("capture"))
            return true;

        if (file.Extension == ".png" &&
            (ScreenResolutions.Contains((width, height)) || ScreenResolutions.Contains((height, width))))
            return true;

        return false;
    }

    private static int ComputeScore(double sharpness, double brightness, int width, int height, List<PhotoFlag> flags)
    {
        // Sharpness contributes most; brightness distance from mid-grey and
        // resolution round it out. Screenshots aren't "bad", just flagged.
        var sharpScore = Math.Clamp(sharpness / 8.0, 0, 60);          // 0..60
        var brightScore = 25 - Math.Min(25, Math.Abs(brightness - 128) / 5.0); // 0..25
        var megapixels = width * (double)height / 1_000_000.0;
        var resScore = Math.Clamp(megapixels * 3, 0, 15);             // 0..15

        var score = sharpScore + brightScore + resScore;
        if (flags.Contains(PhotoFlag.Blurry)) score -= 20;
        return (int)Math.Clamp(score, 0, 100);
    }
}
