namespace DuplicateFinderPro.Core.Models;

/// <summary>
/// One reason a group is considered a duplicate — a detection method plus how
/// strong the match was on that axis (e.g. Name = 0.96, ExactContent = 1.0).
/// </summary>
public sealed record GroupReason(DetectionMethod Method, double Similarity, string Signature);

/// <summary>
/// A cluster of files considered duplicates of one another. A group may match on
/// several axes at once (same name AND identical hash) — those are captured in
/// <see cref="Reasons"/> so the UI can show and filter by them.
/// </summary>
public sealed class DuplicateGroup
{
    public DuplicateGroup(DetectionMethod method, IReadOnlyList<FileItem> files, string signature, double similarity = 1.0)
        : this(files, new[] { new GroupReason(method, similarity, signature) })
    {
    }

    public DuplicateGroup(IReadOnlyList<FileItem> files, IReadOnlyList<GroupReason> reasons)
    {
        Files = files;
        Reasons = reasons.Count > 0 ? reasons : new[] { new GroupReason(DetectionMethod.None, 0, string.Empty) };
        Methods = Reasons.Aggregate(DetectionMethod.None, (acc, r) => acc | r.Method);
    }

    /// <summary>All members of the duplicate cluster (2 or more).</summary>
    public IReadOnlyList<FileItem> Files { get; }

    /// <summary>Why this group is a duplicate (one entry per matching method).</summary>
    public IReadOnlyList<GroupReason> Reasons { get; }

    /// <summary>Combined flags of every method that matched this group.</summary>
    public DetectionMethod Methods { get; }

    /// <summary>Primary (first) method — kept for back-compat/exports.</summary>
    public DetectionMethod Method => Reasons[0].Method;

    /// <summary>Strongest similarity across all reasons.</summary>
    public double Similarity => Reasons.Max(r => r.Similarity);

    public string Signature => Reasons[0].Signature;

    public int Count => Files.Count;

    /// <summary>Bytes that could be reclaimed by keeping a single copy.</summary>
    public long ReclaimableBytes => Files.Count <= 1 ? 0 : Files.Skip(1).Sum(f => f.Length);

    /// <summary>Total size occupied by every member of the group.</summary>
    public long TotalBytes => Files.Sum(f => f.Length);

    public bool HasMethod(DetectionMethod method) => Methods.HasFlag(method);

    /// <summary>Similarity on a specific axis (0 if that method didn't match this group).</summary>
    public double SimilarityFor(DetectionMethod method) =>
        Reasons.Where(r => r.Method == method).Select(r => r.Similarity).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Merges groups that describe the exact same set of files (e.g. found by
    /// both the exact-content and name detectors) into a single multi-reason group.
    /// </summary>
    public static IReadOnlyList<DuplicateGroup> MergeByFileSet(IEnumerable<DuplicateGroup> groups)
    {
        var byKey = new Dictionary<string, List<DuplicateGroup>>();
        foreach (var g in groups)
        {
            var key = string.Join("", g.Files.Select(f => f.FullPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
            if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<DuplicateGroup>();
            list.Add(g);
        }

        var merged = new List<DuplicateGroup>();
        foreach (var list in byKey.Values)
        {
            if (list.Count == 1) { merged.Add(list[0]); continue; }
            var reasons = list.SelectMany(g => g.Reasons)
                              .GroupBy(r => r.Method)
                              .Select(grp => grp.OrderByDescending(r => r.Similarity).First())
                              .ToList();
            merged.Add(new DuplicateGroup(list[0].Files, reasons));
        }
        return merged;
    }
}
