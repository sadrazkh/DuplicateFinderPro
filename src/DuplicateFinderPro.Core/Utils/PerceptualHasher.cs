using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DuplicateFinderPro.Core.Utils;

/// <summary>
/// Computes a 64-bit difference hash (dHash) for images. dHash is robust to
/// resizing, re-encoding and moderate compression, so the same picture saved
/// as JPG and PNG (or at different resolutions) yields a near-identical hash.
/// </summary>
public static class PerceptualHasher
{
    // dHash works on a 9x8 grayscale grid -> 8x8 = 64 comparisons.
    private const int Width = 9;
    private const int Height = 8;

    /// <summary>Computes the dHash of an image stream, or null if it can't be decoded.</summary>
    public static async Task<ulong?> ComputeAsync(Stream stream, CancellationToken ct = default)
    {
        try
        {
            using var image = await Image.LoadAsync<L8>(stream, ct);
            image.Mutate(x => x.Resize(Width, Height));
            return ComputeFromGrayscale(image);
        }
        catch
        {
            // Corrupt/unsupported image — treated as "not hashable".
            return null;
        }
    }

    /// <summary>Computes the dHash of an image file.</summary>
    public static async Task<ulong?> ComputeAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return await ComputeAsync(fs, ct);
        }
        catch
        {
            return null;
        }
    }

    private static ulong ComputeFromGrayscale(Image<L8> image)
    {
        ulong hash = 0;
        var bit = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < Width - 1; x++)
                {
                    if (row[x].PackedValue < row[x + 1].PackedValue)
                        hash |= 1UL << bit;
                    bit++;
                }
            }
        });
        return hash;
    }

    /// <summary>Number of differing bits between two perceptual hashes (0..64).</summary>
    public static int HammingDistance(ulong a, ulong b) => System.Numerics.BitOperations.PopCount(a ^ b);
}
