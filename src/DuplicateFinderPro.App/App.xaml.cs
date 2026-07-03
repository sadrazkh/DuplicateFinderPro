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

        var dialogs = new DialogService();
        var theme = new ThemeManager();
        theme.SetDark(false);

        var viewModel = new MainViewModel(dialogs, theme);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Duplicate Finder Pro", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
