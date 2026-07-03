namespace DuplicateFinderPro.Core.Utils;

/// <summary>Extension-based classification of the media kinds we can fingerprint.</summary>
public static class MediaTypes
{
    public static readonly IReadOnlySet<string> Images = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".tga", ".pbm",
    };

    public static readonly IReadOnlySet<string> Videos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".3gp",
    };

    public static bool IsImage(string extension) => Images.Contains(extension);
    public static bool IsVideo(string extension) => Videos.Contains(extension);
}
