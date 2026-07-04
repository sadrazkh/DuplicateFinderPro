using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace DuplicateFinderPro.Core.Tests;

public sealed class PhotoQualityTests : IDisposable
{
    private readonly string _dir;

    public PhotoQualityTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dfp_photo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private FileItem SaveFlat(string name, byte level)
    {
        using var img = new Image<Rgba32>(800, 600, new Rgba32(level, level, level));
        var path = Path.Combine(_dir, name);
        img.SaveAsPng(path);
        return ToItem(path);
    }

    private FileItem SaveNoise(string name)
    {
        using var img = new Image<Rgba32>(800, 600);
        var rng = new Random(1);
        img.ProcessPixelRows(a =>
        {
            for (var y = 0; y < a.Height; y++)
            {
                var row = a.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var v = (byte)rng.Next(256);
                    row[x] = new Rgba32(v, v, v);
                }
            }
        });
        var path = Path.Combine(_dir, name);
        img.SaveAsPng(path);
        return ToItem(path);
    }

    private static FileItem ToItem(string path)
    {
        var fi = new FileInfo(path);
        return new FileItem(fi.FullName, fi.Length, fi.LastWriteTimeUtc);
    }

    private static ScanOptions Opts() => new();

    [Fact]
    public async Task Flat_mid_grey_image_is_flagged_blurry()
    {
        var item = SaveFlat("flat.png", 128);
        var r = await ImageQualityAnalyzer.AnalyzeAsync(item, Opts(), default);
        Assert.NotNull(r);
        Assert.Contains(PhotoFlag.Blurry, r!.Flags);
    }

    [Fact]
    public async Task Noisy_image_is_not_blurry()
    {
        var item = SaveNoise("noise.png");
        var r = await ImageQualityAnalyzer.AnalyzeAsync(item, Opts(), default);
        Assert.NotNull(r);
        Assert.DoesNotContain(PhotoFlag.Blurry, r!.Flags);
    }

    [Fact]
    public async Task Very_dark_image_is_flagged_dark()
    {
        var item = SaveFlat("dark.png", 8);
        var r = await ImageQualityAnalyzer.AnalyzeAsync(item, Opts(), default);
        Assert.NotNull(r);
        Assert.Contains(PhotoFlag.Dark, r!.Flags);
    }

    [Fact]
    public async Task Reports_dimensions()
    {
        var item = SaveFlat("dim.png", 128);
        var r = await ImageQualityAnalyzer.AnalyzeAsync(item, Opts(), default);
        Assert.Equal(800, r!.Width);
        Assert.Equal(600, r.Height);
    }
}
