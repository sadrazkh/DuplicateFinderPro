using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Utils;
using MaterialDesignThemes.Wpf;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>
/// A selectable "kind of file to scan" preset (Images, Videos, …). Selecting one
/// or more restricts the scan to those extensions, so it never wanders into
/// executables or system files. Selecting none = scan everything.
/// </summary>
public sealed class FileCategoryOption : ObservableObject
{
    private bool _isSelected;

    public FileCategoryOption(string key, PackIconKind icon, IEnumerable<string> extensions)
    {
        Key = key;
        Icon = icon;
        Extensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Localization key + stable id used for persistence.</summary>
    public string Key { get; }
    public PackIconKind Icon { get; }
    public IReadOnlySet<string> Extensions { get; }

    public string Display => Localization.Localization.Instance[$"Cat.{Key}"];

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public static IReadOnlyList<FileCategoryOption> CreateDefault() => new[]
    {
        new FileCategoryOption("Images", PackIconKind.ImageMultipleOutline, MediaTypes.Images),
        new FileCategoryOption("Videos", PackIconKind.MovieOpenOutline, MediaTypes.Videos),
        new FileCategoryOption("Audio", PackIconKind.MusicNote, new[]
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus", ".aiff", ".alac",
        }),
        new FileCategoryOption("Documents", PackIconKind.FileDocumentOutline, new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf",
            ".odt", ".ods", ".odp", ".csv", ".md", ".epub",
        }),
        new FileCategoryOption("Archives", PackIconKind.ZipBoxOutline, new[]
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab",
        }),
    };
}
