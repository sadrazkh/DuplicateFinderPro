using DuplicateFinderPro.App.Localization;
using DuplicateFinderPro.Core.Services;

namespace DuplicateFinderPro.App.Services;

/// <summary>Serializable snapshot of everything worth remembering between runs.</summary>
public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public bool Dark { get; set; }

    public bool UseExactContent { get; set; } = true;
    public bool UseNameSimilarity { get; set; }
    public bool UsePerceptualImage { get; set; }
    public bool UsePerceptualVideo { get; set; }
    public bool AnalyzePhotoQuality { get; set; }
    public double BlurThreshold { get; set; } = 120;

    public bool Recursive { get; set; } = true;
    public bool IncludeHidden { get; set; }
    public long MinSizeKb { get; set; } = 1;
    public long MaxSizeKb { get; set; }
    public string IncludeExtensions { get; set; } = string.Empty;
    public string ExcludeExtensions { get; set; } = string.Empty;

    public double NameThreshold { get; set; } = 0.85;
    public int PerceptualThreshold { get; set; } = 8;
    public int VideoSamples { get; set; } = 12;
    public int VideoIntroSkipPercent { get; set; } = 8;
    public int VideoOutroSkipPercent { get; set; } = 5;
    public bool GentleResourceUsage { get; set; } = true;
    public string FfmpegPath { get; set; } = string.Empty;

    public KeepRule KeepRule { get; set; } = KeepRule.Newest;
    public List<string> Folders { get; set; } = new();

    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
}
