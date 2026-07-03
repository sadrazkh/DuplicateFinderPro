using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.Core.Services;

/// <summary>Rule for deciding which single file to keep within each duplicate group.</summary>
public enum KeepRule
{
    /// <summary>Keep the most recently modified file.</summary>
    Newest,
    /// <summary>Keep the oldest file.</summary>
    Oldest,
    /// <summary>Keep the largest file (useful for higher-quality media).</summary>
    Largest,
    /// <summary>Keep the smallest file.</summary>
    Smallest,
    /// <summary>Keep the file with the shortest full path.</summary>
    ShortestPath,
    /// <summary>Keep the file whose name has no copy markers like "(1)" / "copy".</summary>
    CleanestName,
}

/// <summary>
/// Given a keep-rule, returns the redundant files (everything except the one to
/// keep) for each group — the set a user would typically delete or move.
/// </summary>
public static class DuplicateSelector
{
    public static IReadOnlyList<FileItem> Redundant(DuplicateGroup group, KeepRule rule)
    {
        if (group.Count <= 1) return Array.Empty<FileItem>();
        var keep = SelectKeeper(group.Files, rule);
        return group.Files.Where(f => !ReferenceEquals(f, keep)).ToList();
    }

    public static FileItem SelectKeeper(IReadOnlyList<FileItem> files, KeepRule rule) => rule switch
    {
        KeepRule.Newest => files.MaxBy(f => f.LastWriteUtc)!,
        KeepRule.Oldest => files.MinBy(f => f.LastWriteUtc)!,
        KeepRule.Largest => files.MaxBy(f => f.Length)!,
        KeepRule.Smallest => files.MinBy(f => f.Length)!,
        KeepRule.ShortestPath => files.MinBy(f => f.FullPath.Length)!,
        KeepRule.CleanestName => files
            .OrderBy(f => CopyMarkerScore(f.FileName))
            .ThenBy(f => f.FileName.Length)
            .First(),
        _ => files[0],
    };

    private static int CopyMarkerScore(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var score = 0;
        if (lower.Contains("copy") || lower.Contains("copie") || lower.Contains("kopya")) score += 2;
        if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"\(\d+\)")) score += 1;
        if (lower.Contains(" - ")) score += 1;
        return score;
    }
}
