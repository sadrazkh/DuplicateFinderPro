using System.Collections.ObjectModel;
using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Models;
using MaterialDesignThemes.Wpf;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>One "why this is a duplicate" chip on a group (method + strength).</summary>
public sealed class ReasonChipViewModel : ObservableObject
{
    public ReasonChipViewModel(GroupReason reason)
    {
        Method = reason.Method;
        Similarity = reason.Similarity;
    }

    public DetectionMethod Method { get; }
    public double Similarity { get; }

    public PackIconKind Icon => Method switch
    {
        DetectionMethod.ExactContent => PackIconKind.Fingerprint,
        DetectionMethod.NameSimilarity => PackIconKind.FormTextbox,
        DetectionMethod.PerceptualImage => PackIconKind.ImageMultipleOutline,
        DetectionMethod.PerceptualVideo => PackIconKind.MovieOpenOutline,
        _ => PackIconKind.HelpCircleOutline,
    };

    public string Text => $"{MethodName(Method)} · {Similarity:P0}";

    public void RefreshLocalized() => OnPropertyChanged(nameof(Text));

    private static string MethodName(DetectionMethod m) => m switch
    {
        DetectionMethod.ExactContent => Localization.Localization.Instance["Method.ExactContent"],
        DetectionMethod.NameSimilarity => Localization.Localization.Instance["Method.NameSimilarity"],
        DetectionMethod.PerceptualImage => Localization.Localization.Instance["Method.PerceptualImage"],
        DetectionMethod.PerceptualVideo => Localization.Localization.Instance["Method.PerceptualVideo"],
        _ => m.ToString(),
    };
}

/// <summary>A duplicate cluster shown as an expandable card in the results list.</summary>
public sealed class DuplicateGroupViewModel : ObservableObject
{
    private bool _isExpanded;
    private bool _isVisible = true;

    public DuplicateGroupViewModel(int index, DuplicateGroup model)
    {
        Index = index;
        Model = model;
        Files = new ObservableCollection<FileItemViewModel>(model.Files.Select(f => new FileItemViewModel(f)));
        ReasonChips = model.Reasons
            .OrderByDescending(r => r.Similarity)
            .Select(r => new ReasonChipViewModel(r))
            .ToList();
    }

    public int Index { get; }
    public DuplicateGroup Model { get; }
    public ObservableCollection<FileItemViewModel> Files { get; }
    public IReadOnlyList<ReasonChipViewModel> ReasonChips { get; }

    public int Count => Model.Count;
    public long ReclaimableBytes => Model.ReclaimableBytes;
    public double Similarity => Model.Similarity;
    public string Signature => Model.Signature;
    public DetectionMethod Methods => Model.Methods;

    public string SimilarityDisplay => Similarity >= 0.999 ? "100%" : $"{Similarity * 100:0}%";

    /// <summary>Localized name of the primary method (for compact lists).</summary>
    public string MethodDisplay => ReasonChips.Count > 0
        ? ReasonChips[0].Text.Split('·')[0].Trim()
        : string.Empty;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Whether this group survives the current results filter.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public void RefreshLocalized()
    {
        foreach (var c in ReasonChips) c.RefreshLocalized();
        OnPropertyChanged(nameof(MethodDisplay));
    }
}
