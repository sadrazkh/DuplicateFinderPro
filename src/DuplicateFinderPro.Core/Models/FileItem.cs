namespace DuplicateFinderPro.Core.Models;

/// <summary>
/// A single file discovered during a scan, together with the metadata and
/// lazily-computed signatures used by the detection strategies.
/// </summary>
public sealed class FileItem
{
    public FileItem(string fullPath, long length, DateTime lastWriteUtc)
    {
        FullPath = fullPath;
        Length = length;
        LastWriteUtc = lastWriteUtc;
        FileName = Path.GetFileName(fullPath);
        Extension = Path.GetExtension(fullPath).ToLowerInvariant();
        DirectoryName = Path.GetDirectoryName(fullPath) ?? string.Empty;
    }

    public string FullPath { get; }
    public string FileName { get; }
    public string DirectoryName { get; }
    public string Extension { get; }
    public long Length { get; }
    public DateTime LastWriteUtc { get; }

    /// <summary>Full SHA-256 of the content, computed on demand by the exact detector.</summary>
    public string? ContentHash { get; set; }

    /// <summary>64-bit perceptual hash (image/video), computed on demand.</summary>
    public ulong? PerceptualHash { get; set; }

    /// <summary>Normalized name used by the fuzzy name matcher.</summary>
    public string? NormalizedName { get; set; }

    public override string ToString() => FullPath;
}
