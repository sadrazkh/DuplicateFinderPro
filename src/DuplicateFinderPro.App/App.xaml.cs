using System.Windows;
using System.Windows.Threading;
using DuplicateFinderPro.App.Services;
using DuplicateFinderPro.App.ViewModels;
using DuplicateFinderPro.App.Views;

namespace DuplicateFinderPro.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        var settings = SettingsStore.Load();

        var dialogs = new DialogService();
        var theme = new ThemeManager();
        theme.SetDark(settings.Dark);
        Localization.Localization.Instance.Language = settings.Language;

        var viewModel = new MainViewModel(dialogs, theme);
        viewModel.ApplySettings(settings);

        var window = new MainWindow { DataContext = viewModel };
        if (settings.WindowWidth >= 900 && settings.WindowHeight >= 600)
        {
            window.Width = settings.WindowWidth;
            window.Height = settings.WindowHeight;
        }

        window.Closing += (_, _) =>
        {
            viewModel.CaptureSettings(settings);
            settings.Language = Localization.Localization.Instance.Language;
            settings.Dark = theme.IsDark;
            settings.WindowWidth = window.ActualWidth;
            settings.WindowHeight = window.ActualHeight;
            SettingsStore.Save(settings);
        };

        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Duplicate Finder Pro", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
