using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>A single analysed photo shown in the Photo Cleanup tab.</summary>
public sealed class PhotoIssueViewModel : ObservableObject
{
    private bool _isSelected;

    public PhotoIssueViewModel(ImageQualityResult model) => Model = model;

    public ImageQualityResult Model { get; }

    public string FullPath => Model.File.FullPath;
    public string FileName => Model.File.FileName;
    public string DirectoryName => Model.File.DirectoryName;
    public string Extension => Model.File.Extension;
    public long Length => Model.Length;
    public DateTime LastWriteUtc => Model.File.LastWriteUtc;

    public int Score => Model.Score;
    public string Resolution => Model.Resolution;
    public string Megapixels => Model.Megapixels;
    public double Sharpness => Model.Sharpness;
    public double Brightness => Model.Brightness;
    public IReadOnlyList<PhotoFlag> Flags => Model.Flags;
    public bool HasIssues => Model.HasIssues;

    public string FlagsText => Model.Flags.Count == 0
        ? "OK"
        : string.Join("  ·  ", Model.Flags.Select(FlagLabel));

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void RefreshLocalized() => OnPropertyChanged(nameof(FlagsText));

    public static string FlagLabel(PhotoFlag flag) =>
        Localization.Localization.Instance[$"Photo.{flag}"];
}
