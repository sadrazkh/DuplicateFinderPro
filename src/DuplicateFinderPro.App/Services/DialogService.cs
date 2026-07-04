using System.Windows;
using Microsoft.Win32;

namespace DuplicateFinderPro.App.Services;

/// <summary>Thin wrapper over the common Win32 dialogs and message boxes.</summary>
public sealed class DialogService
{
    public string? PickFolder(string? title = null)
    {
        var dlg = new OpenFolderDialog
        {
            Title = title ?? "Select folder",
            Multiselect = false,
        };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    public IReadOnlyList<string> PickFolders(string? title = null)
    {
        var dlg = new OpenFolderDialog
        {
            Title = title ?? "Select folders",
            Multiselect = true,
        };
        return dlg.ShowDialog() == true ? dlg.FolderNames : Array.Empty<string>();
    }

    public string? OpenFile(string filter)
    {
        var dlg = new OpenFileDialog { Filter = filter, CheckFileExists = true, Multiselect = false };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string filter, string defaultExt, string? suggestedName = null)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = suggestedName ?? "report",
            AddExtension = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public bool Confirm(string message, string caption)
        => MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void Info(string message, string caption)
        => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warn(string message, string caption)
        => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
}
