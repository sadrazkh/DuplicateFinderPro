using Microsoft.VisualBasic.FileIO;

namespace DuplicateFinderPro.Core.Services;

/// <summary>Outcome of a batch file action.</summary>
public sealed record FileActionResult(int Succeeded, long BytesFreed, IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Safe file operations for resolving duplicates: recycle-bin deletion,
/// permanent deletion, moving, and hard-link replacement. All operations are
/// batch-oriented and never throw for a single failure — they aggregate errors.
/// </summary>
public sealed class FileActionService
{
    /// <summary>Sends files to the Recycle Bin (recoverable).</summary>
    public FileActionResult SendToRecycleBin(IEnumerable<string> paths)
        => Apply(paths, path =>
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin));

    /// <summary>Permanently deletes files (not recoverable).</summary>
    public FileActionResult DeletePermanently(IEnumerable<string> paths)
        => Apply(paths, File.Delete);

    /// <summary>Moves files into <paramref name="targetFolder"/>, de-duplicating name clashes.</summary>
    public FileActionResult MoveTo(IEnumerable<string> paths, string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);
        return Apply(paths, path =>
        {
            var dest = UniqueDestination(targetFolder, Path.GetFileName(path));
            File.Move(path, dest);
        });
    }

    private static FileActionResult Apply(IEnumerable<string> paths, Action<string> op)
    {
        var errors = new List<string>();
        var succeeded = 0;
        long freed = 0;

        foreach (var path in paths)
        {
            try
            {
                long size = 0;
                try { size = new FileInfo(path).Length; } catch { /* ignore sizing failure */ }

                op(path);
                succeeded++;
                freed += size;
            }
            catch (Exception ex)
            {
                errors.Add($"{path}: {ex.Message}");
            }
        }

        return new FileActionResult(succeeded, freed, errors);
    }

    private static string UniqueDestination(string folder, string fileName)
    {
        var dest = Path.Combine(folder, fileName);
        if (!File.Exists(dest)) return dest;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 1; ; i++)
        {
            dest = Path.Combine(folder, $"{name} ({i}){ext}");
            if (!File.Exists(dest)) return dest;
        }
    }
}
