namespace DuplicateFinderPro.App.ViewModels;

/// <summary>How aggressively the scan may use CPU and disk.</summary>
public enum ResourceUsageLevel
{
    /// <summary>Use everything — fastest, but the machine (and an HDD) will be busy.</summary>
    Maximum,
    /// <summary>Half the cores — a good default that stays responsive.</summary>
    Balanced,
    /// <summary>Single-threaded, low priority — gentle on the disk (best for HDDs).</summary>
    Light,
}

/// <summary>Combo-box item pairing a level with its localized label + description.</summary>
public sealed class ResourceOption
{
    public ResourceOption(ResourceUsageLevel level, string labelKey, string descKey)
    {
        Level = level;
        LabelKey = labelKey;
        DescKey = descKey;
    }

    public ResourceUsageLevel Level { get; }
    public string LabelKey { get; }
    public string DescKey { get; }

    public string Label => Localization.Localization.Instance[LabelKey];
    public string Description => Localization.Localization.Instance[DescKey];

    public static IReadOnlyList<ResourceOption> All { get; } = new[]
    {
        new ResourceOption(ResourceUsageLevel.Maximum, "Res.Maximum", "Res.Maximum.Desc"),
        new ResourceOption(ResourceUsageLevel.Balanced, "Res.Balanced", "Res.Balanced.Desc"),
        new ResourceOption(ResourceUsageLevel.Light, "Res.Light", "Res.Light.Desc"),
    };
}
