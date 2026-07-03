using System.Security.Cryptography;

namespace DuplicateFinderPro.Core.Services;

/// <summary>
/// Streaming SHA-256 hashing with a cheap "quick hash" pre-filter (head + tail
/// sample) so we only pay for a full read when files really look identical.
/// </summary>
public static class ContentHasher
{
    private const int QuickSampleBytes = 64 * 1024;
    private const int BufferSize = 1 << 20; // 1 MiB

    /// <summary>Hashes a small head+tail sample — cheap way to split same-size files.</summary>
    public static async Task<string> QuickHashAsync(string path, long length, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[QuickSampleBytes];
        var read = await ReadBlockAsync(fs, buffer, ct);
        sha.AppendData(buffer, 0, read);

        if (length > QuickSampleBytes * 2L)
        {
            fs.Seek(-QuickSampleBytes, SeekOrigin.End);
            read = await ReadBlockAsync(fs, buffer, ct);
            sha.AppendData(buffer, 0, read);
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    /// <summary>Full SHA-256 over the entire file.</summary>
    public static async Task<string> FullHashAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[BufferSize];
        int read;
        while ((read = await fs.ReadAsync(buffer, ct)) > 0)
            sha.AppendData(buffer, 0, read);

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static async Task<int> ReadBlockAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
