using MaterialDesignThemes.Wpf;

namespace DuplicateFinderPro.App.Services;

/// <summary>Toggles the Material Design light/dark base theme at runtime.</summary>
public sealed class ThemeManager
{
    private readonly PaletteHelper _palette = new();

    public bool IsDark { get; private set; }

    public void SetDark(bool dark)
    {
        IsDark = dark;
        var theme = _palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        _palette.SetTheme(theme);
    }

    public void Toggle() => SetDark(!IsDark);
}
