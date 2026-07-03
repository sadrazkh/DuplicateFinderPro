using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Services;
using DuplicateFinderPro.Core.Utils;
using Xunit;

namespace DuplicateFinderPro.Core.Tests;

public sealed class DetectionTests : IDisposable
{
    private readonly string _root;

    public DetectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dfp_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<ScanResult> ScanAsync(DetectionMethod methods, Action<ScanOptions>? tweak = null)
    {
        var options = new ScanOptions
        {
            RootFolders = { _root },
            Methods = methods,
            MinFileSizeBytes = 0,
        };
        tweak?.Invoke(options);
        var progress = new Progress<ScanProgress>();
        return await new DuplicateScanEngine().ScanAsync(options, progress, CancellationToken.None);
    }

    [Fact]
    public async Task ExactContent_finds_identical_bytes_under_different_names()
    {
        Write("a/movie.bin", "the same exact content 12345");
        Write("b/completely-different-name.bin", "the same exact content 12345");
        Write("c/unique.bin", "i am unique");

        var result = await ScanAsync(DetectionMethod.ExactContent);

        var group = Assert.Single(result.Groups);
        Assert.Equal(2, group.Count);
        Assert.Equal(1.0, group.Similarity);
    }

    [Fact]
    public async Task ExactContent_finds_duplicates_across_deeply_nested_folders()
    {
        // The same movie dropped into two completely separate branch trees.
        Write(@"Movies\Action\rip1\the.movie.1080p.mkv", "IDENTICAL MOVIE BYTES");
        Write(@"Downloads\torrents\Some Folder\another name.mkv", "IDENTICAL MOVIE BYTES");
        Write(@"Backup\2024\misc\unrelated.mkv", "different bytes here");

        var result = await ScanAsync(DetectionMethod.ExactContent);

        var group = Assert.Single(result.Groups);
        Assert.Equal(2, group.Count);
        // Confirm the two hits really live in different directories.
        var dirs = group.Files.Select(f => f.DirectoryName).Distinct().Count();
        Assert.Equal(2, dirs);
    }

    [Fact]
    public async Task ExactContent_ignores_same_size_but_different_content()
    {
        Write("a.bin", "AAAAAAAAAA"); // 10 chars
        Write("b.bin", "BBBBBBBBBB"); // 10 chars, same size, different bytes

        var result = await ScanAsync(DetectionMethod.ExactContent);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task NameSimilarity_groups_copy_markers()
    {
        Write("Big Buck Bunny.mkv", "x");
        Write("Big Buck Bunny (1).mkv", "y");     // different content on purpose
        Write("Big Buck Bunny - Copy.mkv", "z");
        Write("Totally Other Film.mkv", "w");

        var result = await ScanAsync(DetectionMethod.NameSimilarity, o => o.NameSimilarityThreshold = 0.8);

        var group = Assert.Single(result.Groups);
        Assert.Equal(3, group.Count);
    }

    [Fact]
    public void Normalize_collapses_copy_variants()
    {
        Assert.Equal(
            StringSimilarity.Normalize("Movie"),
            StringSimilarity.Normalize("Movie (1)"));
        Assert.Equal(
            StringSimilarity.Normalize("Movie"),
            StringSimilarity.Normalize("Movie - Copy"));
    }

    [Fact]
    public void Selector_keeps_the_requested_file()
    {
        var older = new FileItem(@"C:\x\old.txt", 100, DateTime.UtcNow.AddDays(-2));
        var newer = new FileItem(@"C:\x\new.txt", 100, DateTime.UtcNow);
        var group = new DuplicateGroup(DetectionMethod.ExactContent, new[] { older, newer }, "sig");

        var redundantWhenKeepingNewest = DuplicateSelector.Redundant(group, KeepRule.Newest);
        Assert.Single(redundantWhenKeepingNewest);
        Assert.Equal(older.FullPath, redundantWhenKeepingNewest[0].FullPath);
    }

    [Fact]
    public void ByteSize_humanizes()
    {
        Assert.Equal("1 KB", ByteSize.Humanize(1024, System.Globalization.CultureInfo.InvariantCulture));
        Assert.StartsWith("1.5", ByteSize.Humanize(1536, System.Globalization.CultureInfo.InvariantCulture));
    }
}
