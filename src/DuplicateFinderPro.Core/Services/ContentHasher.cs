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

    /// <summary>
    /// Full SHA-256 over the entire file. Optionally reports byte progress
    /// (0..1) so the UI can show a bar for large files.
    /// </summary>
    public static async Task<string> FullHashAsync(string path, CancellationToken ct, IProgress<double>? fileProgress = null, long length = 0)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var total = length > 0 ? length : fs.Length;
        var buffer = new byte[BufferSize];
        long done = 0;
        long lastReport = 0;
        int read;
        while ((read = await fs.ReadAsync(buffer, ct)) > 0)
        {
            sha.AppendData(buffer, 0, read);
            done += read;
            // Throttle: only ping every ~4 MiB (and only for files worth showing).
            if (fileProgress is not null && total > 8 * 1024 * 1024 && done - lastReport >= 4 * 1024 * 1024)
            {
                lastReport = done;
                fileProgress.Report(Math.Min(1.0, (double)done / total));
            }
        }

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
