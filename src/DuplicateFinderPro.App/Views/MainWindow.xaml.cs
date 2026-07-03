using System.IO;
using System.Windows;
using DuplicateFinderPro.App.ViewModels;

namespace DuplicateFinderPro.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnFoldersDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFoldersDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var path in paths)
        {
            // Accept dropped folders, or resolve a dropped file to its folder.
            if (Directory.Exists(path))
                ViewModel.AddFolder(path);
            else if (File.Exists(path))
                ViewModel.AddFolder(Path.GetDirectoryName(path)!);
        }
    }
}
