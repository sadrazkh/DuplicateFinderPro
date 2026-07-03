using System.Collections.ObjectModel;
using DuplicateFinderPro.App.Localization;
using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>A duplicate cluster shown as an expandable card in the results list.</summary>
public sealed class DuplicateGroupViewModel : ObservableObject
{
    private bool _isExpanded = true;
    private bool _isVisible = true;

    public DuplicateGroupViewModel(int index, DuplicateGroup model)
    {
        Index = index;
        Model = model;
        Files = new ObservableCollection<FileItemViewModel>(model.Files.Select(f => new FileItemViewModel(f)));
    }

    public int Index { get; }
    public DuplicateGroup Model { get; }
    public ObservableCollection<FileItemViewModel> Files { get; }

    public int Count => Model.Count;
    public long ReclaimableBytes => Model.ReclaimableBytes;
    public double Similarity => Model.Similarity;
    public string Signature => Model.Signature;
    public DetectionMethod Method => Model.Method;

    public string MethodKey => Method switch
    {
        DetectionMethod.ExactContent => "Method.ExactContent",
        DetectionMethod.NameSimilarity => "Method.NameSimilarity",
        DetectionMethod.PerceptualImage => "Method.PerceptualImage",
        DetectionMethod.PerceptualVideo => "Method.PerceptualVideo",
        _ => "Config.Methods",
    };

    public string MethodDisplay => Localization.Localization.Instance[MethodKey];

    public string SimilarityDisplay => Similarity >= 0.999 ? "100%" : $"{Similarity * 100:0}%";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Whether this group survives the current results filter.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Hides the group unless a file name or path contains the filter text.</summary>
    public void ApplyFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            IsVisible = true;
            return;
        }
        IsVisible = Files.Any(f =>
            f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            f.DirectoryName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
