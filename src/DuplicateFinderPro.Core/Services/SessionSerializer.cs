using System.Text.Json;
using System.Text.Json.Serialization;
using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.Core.Services;

/// <summary>
/// Saves and loads a scan's duplicate groups to a native <c>.dfp.json</c> file
/// so a user can revisit found duplicates later (or on another machine) without
/// re-scanning. Files that no longer exist are still loaded and can be flagged.
/// </summary>
public static class SessionSerializer
{
    private sealed record SessionFile(int Version, DateTime SavedAtUtc, List<SessionGroup> Groups);
    private sealed record SessionGroup(List<SessionReason> Reasons, List<SessionItem> Files);
    private sealed record SessionReason(DetectionMethod Method, double Similarity, string Signature);
    private sealed record SessionItem(string Path, long Length, DateTime LastWriteUtc, string? ContentHash);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task SaveAsync(IReadOnlyList<DuplicateGroup> groups, string path, CancellationToken ct = default)
    {
        var session = new SessionFile(
            Version: 1,
            SavedAtUtc: DateTime.UtcNow,
            Groups: groups.Select(g => new SessionGroup(
                g.Reasons.Select(r => new SessionReason(r.Method, r.Similarity, r.Signature)).ToList(),
                g.Files.Select(f => new SessionItem(f.FullPath, f.Length, f.LastWriteUtc, f.ContentHash)).ToList()))
                .ToList());

        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, session, Options, ct);
    }

    public static async Task<IReadOnlyList<DuplicateGroup>> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(path);
        var session = await JsonSerializer.DeserializeAsync<SessionFile>(fs, Options, ct)
                      ?? throw new InvalidDataException("Empty or invalid session file.");

        var groups = new List<DuplicateGroup>();
        foreach (var g in session.Groups)
        {
            var files = g.Files.Select(i => new FileItem(i.Path, i.Length, i.LastWriteUtc) { ContentHash = i.ContentHash }).ToList();
            if (files.Count < 2) continue;
            var reasons = g.Reasons.Count > 0
                ? g.Reasons.Select(r => new GroupReason(r.Method, r.Similarity, r.Signature)).ToList()
                : new List<GroupReason> { new(DetectionMethod.ExactContent, 1.0, string.Empty) };
            groups.Add(new DuplicateGroup(files, reasons));
        }
        return groups;
    }
}
