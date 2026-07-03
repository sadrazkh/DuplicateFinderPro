using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace DuplicateFinderPro.App.Localization;

public enum AppLanguage { Persian, English }

/// <summary>
/// Runtime localization service. XAML binds to the indexer
/// (<c>{Binding [Some.Key], Source={x:Static loc:Localization.Instance}}</c>);
/// switching language raises a change for "Item[]" so every bound string and the
/// window <see cref="FlowDirection"/> update live without a restart.
/// </summary>
public sealed class Localization : INotifyPropertyChanged
{
    public static Localization Instance { get; } = new();

    private IReadOnlyDictionary<string, string> _table = Strings.Fa;
    private AppLanguage _language = AppLanguage.Persian;

    private Localization() => Apply(AppLanguage.Persian);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Indexer used by bindings; returns the key itself if untranslated.</summary>
    public string this[string key] => _table.TryGetValue(key, out var value) ? value : key;

    public AppLanguage Language
    {
        get => _language;
        set { if (_language != value) Apply(value); }
    }

    public FlowDirection FlowDirection =>
        _language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public bool IsPersian => _language == AppLanguage.Persian;

    /// <summary>Formats a template string looked up by key with arguments.</summary>
    public string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, this[key], args);

    private void Apply(AppLanguage language)
    {
        _language = language;
        _table = language == AppLanguage.Persian ? Strings.Fa : Strings.En;

        var culture = language == AppLanguage.Persian
            ? new CultureInfo("fa-IR")
            : new CultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Notify all bound strings and derived properties.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPersian)));
    }
}
