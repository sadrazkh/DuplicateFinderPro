using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Services;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>Combo-box item pairing a <see cref="KeepRule"/> with its display key.</summary>
public sealed class KeepRuleOption : ObservableObject
{
    public KeepRuleOption(KeepRule rule, string displayKey)
    {
        Rule = rule;
        DisplayKey = displayKey;
    }

    public KeepRule Rule { get; }
    public string DisplayKey { get; }

    public string Display => Localization.Localization.Instance[DisplayKey];

    public void RefreshLocalized() => OnPropertyChanged(nameof(Display));

    public static IReadOnlyList<KeepRuleOption> All { get; } = new[]
    {
        new KeepRuleOption(KeepRule.Newest, "Keep.Newest"),
        new KeepRuleOption(KeepRule.Oldest, "Keep.Oldest"),
        new KeepRuleOption(KeepRule.Largest, "Keep.Largest"),
        new KeepRuleOption(KeepRule.Smallest, "Keep.Smallest"),
        new KeepRuleOption(KeepRule.ShortestPath, "Keep.ShortestPath"),
        new KeepRuleOption(KeepRule.CleanestName, "Keep.CleanestName"),
    };
}
