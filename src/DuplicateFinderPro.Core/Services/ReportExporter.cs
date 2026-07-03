using System.Globalization;
using System.Text;
using System.Text.Json;
using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.Core.Services;

/// <summary>Exports a scan result to CSV or JSON for auditing/record keeping.</summary>
public static class ReportExporter
{
    public static async Task ExportCsvAsync(ScanResult result, string path, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("GroupId,Method,Similarity,FilePath,SizeBytes,SizeHuman,LastModifiedUtc,ReclaimableBytes");

        var groupId = 0;
        foreach (var group in result.Groups)
        {
            groupId++;
            foreach (var file in group.Files)
            {
                sb.Append(groupId).Append(',')
                  .Append(group.Method).Append(',')
                  .Append(group.Similarity.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                  .Append(Csv(file.FullPath)).Append(',')
                  .Append(file.Length).Append(',')
                  .Append(Csv(ByteSize.Humanize(file.Length, CultureInfo.InvariantCulture))).Append(',')
                  .Append(file.LastWriteUtc.ToString("o", CultureInfo.InvariantCulture)).Append(',')
                  .Append(group.ReclaimableBytes)
                  .AppendLine();
            }
        }

        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true), ct);
    }

    public static async Task ExportJsonAsync(ScanResult result, string path, CancellationToken ct = default)
    {
        var payload = new
        {
            summary = new
            {
                filesScanned = result.FilesScanned,
                bytesScanned = result.BytesScanned,
                duplicateGroups = result.Groups.Count,
                redundantFiles = result.RedundantFileCount,
                reclaimableBytes = result.ReclaimableBytes,
                elapsedSeconds = result.Elapsed.TotalSeconds,
            },
            warnings = result.Warnings,
            groups = result.Groups.Select((g, i) => new
            {
                id = i + 1,
                method = g.Method.ToString(),
                similarity = g.Similarity,
                signature = g.Signature,
                reclaimableBytes = g.ReclaimableBytes,
                files = g.Files.Select(f => new
                {
                    path = f.FullPath,
                    sizeBytes = f.Length,
                    lastModifiedUtc = f.LastWriteUtc,
                    contentHash = f.ContentHash,
                }),
            }),
        };

        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, payload, new JsonSerializerOptions { WriteIndented = true }, ct);
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }
}
