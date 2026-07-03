using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.Core.Services;

/// <summary>
/// Recursively enumerates files under the configured roots, applying the size,
/// extension, hidden and folder-exclusion filters. Enumeration is resilient:
/// inaccessible directories are recorded as warnings rather than aborting.
/// </summary>
public sealed class FileScanner
{
    public IReadOnlyList<string> Warnings => _warnings;
    private readonly List<string> _warnings = new();

    public IEnumerable<FileItem> Enumerate(ScanOptions options, CancellationToken ct)
    {
        _warnings.Clear();
        var excluded = new HashSet<string>(
            options.ExcludedFolders.Select(NormalizeDir),
            StringComparer.OrdinalIgnoreCase);

        foreach (var root in options.RootFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
            {
                _warnings.Add($"Folder not found: {root}");
                continue;
            }

            foreach (var item in Walk(root, options, excluded, ct))
                yield return item;
        }
    }

    private IEnumerable<FileItem> Walk(string dir, ScanOptions options, HashSet<string> excluded, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (excluded.Contains(NormalizeDir(dir)))
            yield break;

        string[] files;
        try
        {
            files = Directory.GetFiles(dir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _warnings.Add($"Skipped (no access): {dir}");
            yield break;
        }

        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            FileItem? item = TryCreateItem(path, options);
            if (item is not null)
                yield return item;
        }

        if (!options.Recursive)
            yield break;

        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(dir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _warnings.Add($"Skipped (no access): {dir}");
            yield break;
        }

        foreach (var sub in subDirs)
        {
            if (!options.IncludeHidden && IsHiddenOrSystem(sub))
                continue;

            foreach (var item in Walk(sub, options, excluded, ct))
                yield return item;
        }
    }

    private FileItem? TryCreateItem(string path, ScanOptions options)
    {
        try
        {
            var info = new FileInfo(path);

            if (!options.IncludeHidden &&
                (info.Attributes.HasFlag(FileAttributes.Hidden) || info.Attributes.HasFlag(FileAttributes.System)))
                return null;

            if (options.MinFileSizeBytes > 0 && info.Length < options.MinFileSizeBytes) return null;
            if (options.MaxFileSizeBytes > 0 && info.Length > options.MaxFileSizeBytes) return null;

            var ext = info.Extension.ToLowerInvariant();
            if (options.ExcludeExtensions.Contains(ext)) return null;
            if (options.IncludeExtensions.Count > 0 && !options.IncludeExtensions.Contains(ext)) return null;

            return new FileItem(info.FullName, info.Length, info.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or FileNotFoundException)
        {
            _warnings.Add($"Skipped file: {path}");
            return null;
        }
    }

    private static bool IsHiddenOrSystem(string dir)
    {
        try
        {
            var attr = new DirectoryInfo(dir).Attributes;
            return attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeDir(string dir) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
}
