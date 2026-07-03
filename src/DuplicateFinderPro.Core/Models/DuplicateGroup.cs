namespace DuplicateFinderPro.Core.Models;

/// <summary>
/// A cluster of files considered duplicates of one another by a given method.
/// </summary>
public sealed class DuplicateGroup
{
    public DuplicateGroup(DetectionMethod method, IReadOnlyList<FileItem> files, string signature, double similarity = 1.0)
    {
        Method = method;
        Files = files;
        Signature = signature;
        Similarity = similarity;
    }

    public DetectionMethod Method { get; }

    /// <summary>All members of the duplicate cluster (2 or more).</summary>
    public IReadOnlyList<FileItem> Files { get; }

    /// <summary>Human-readable key that produced the match (hash, name, etc.).</summary>
    public string Signature { get; }

    /// <summary>Match confidence 0..1 (1 = exact).</summary>
    public double Similarity { get; }

    public int Count => Files.Count;

    /// <summary>Bytes that could be reclaimed by keeping a single copy.</summary>
    public long ReclaimableBytes => Files.Count <= 1 ? 0 : Files.Skip(1).Sum(f => f.Length);

    /// <summary>Total size occupied by every member of the group.</summary>
    public long TotalBytes => Files.Sum(f => f.Length);
}
