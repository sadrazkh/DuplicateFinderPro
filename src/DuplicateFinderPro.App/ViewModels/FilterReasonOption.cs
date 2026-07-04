using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Models;
using MaterialDesignThemes.Wpf;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>A selectable "match reason" used to filter results after a scan.</summary>
public sealed class FilterReasonOption : ObservableObject
{
    private bool _isSelected;

    public FilterReasonOption(DetectionMethod method, string labelKey, PackIconKind icon)
    {
        Method = method;
        LabelKey = labelKey;
        Icon = icon;
    }

    public DetectionMethod Method { get; }
    public string LabelKey { get; }
    public PackIconKind Icon { get; }

    public string Display => Localization.Localization.Instance[LabelKey];

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void RefreshLocalized() => OnPropertyChanged(nameof(Display));

    public static IReadOnlyList<FilterReasonOption> CreateDefault() => new[]
    {
        new FilterReasonOption(DetectionMethod.ExactContent, "Method.ExactContent", PackIconKind.Fingerprint),
        new FilterReasonOption(DetectionMethod.NameSimilarity, "Method.NameSimilarity", PackIconKind.FormTextbox),
        new FilterReasonOption(DetectionMethod.PerceptualImage, "Method.PerceptualImage", PackIconKind.ImageMultipleOutline),
        new FilterReasonOption(DetectionMethod.PerceptualVideo, "Method.PerceptualVideo", PackIconKind.MovieOpenOutline),
    };
}
