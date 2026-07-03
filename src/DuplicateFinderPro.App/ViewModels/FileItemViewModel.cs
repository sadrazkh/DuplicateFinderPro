using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.Core.Models;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>A single file row inside a duplicate group.</summary>
public sealed class FileItemViewModel : ObservableObject
{
    private bool _isMarkedForRemoval;

    public FileItemViewModel(FileItem model) => Model = model;

    public FileItem Model { get; }

    public string FileName => Model.FileName;
    public string DirectoryName => Model.DirectoryName;
    public string FullPath => Model.FullPath;
    public long Length => Model.Length;
    public DateTime LastWriteUtc => Model.LastWriteUtc;

    /// <summary>When true, this file is a redundant copy the user wants removed.</summary>
    public bool IsMarkedForRemoval
    {
        get => _isMarkedForRemoval;
        set
        {
            if (SetProperty(ref _isMarkedForRemoval, value))
                OnPropertyChanged(nameof(IsKeeper));
        }
    }

    public bool IsKeeper => !_isMarkedForRemoval;
}
