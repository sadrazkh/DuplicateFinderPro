using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using DuplicateFinderPro.App.Localization;
using DuplicateFinderPro.App.Mvvm;
using DuplicateFinderPro.App.Services;
using DuplicateFinderPro.Core.Models;
using DuplicateFinderPro.Core.Services;
using DuplicateFinderPro.Core.Utils;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>The single view-model backing the main window.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly DialogService _dialogs;
    private readonly ThemeManager _theme;
    private readonly FileActionService _fileActions = new();
    private readonly FfmpegInstaller _ffmpegInstaller = new();
    private CancellationTokenSource? _cts;

    public MainViewModel(DialogService dialogs, ThemeManager theme)
    {
        _dialogs = dialogs;
        _theme = theme;

        AddFolderCommand = new RelayCommand(_ => AddFolders());
        RemoveFolderCommand = new RelayCommand(p => RemoveFolder(p as string));
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsScanning);
        AutoSelectCommand = new RelayCommand(_ => AutoSelect(), _ => HasResults);
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection(), _ => HasResults);
        RecycleCommand = new RelayCommand(_ => RecycleSelected(), _ => SelectedCount > 0);
        DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedCount > 0);
        MoveCommand = new RelayCommand(_ => MoveSelected(), _ => SelectedCount > 0);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, () => HasResults);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync, () => HasResults);
        OpenLocationCommand = new RelayCommand(p => OpenLocation(p as FileItemViewModel));
        OpenFileCommand = new RelayCommand(p => OpenFile(p as FileItemViewModel));
        CopyPathCommand = new RelayCommand(p => CopyToClipboard((p as FileItemViewModel)?.FullPath));
        CopyFolderCommand = new RelayCommand(p => CopyToClipboard((p as FileItemViewModel)?.DirectoryName));
        SelectGroupRedundantCommand = new RelayCommand(p => SelectGroupRedundant(p as DuplicateGroupViewModel));
        ClearGroupSelectionCommand = new RelayCommand(p => ClearGroupSelection(p as DuplicateGroupViewModel));
        ExpandAllCommand = new RelayCommand(_ => SetExpanded(true), _ => HasResults);
        CollapseAllCommand = new RelayCommand(_ => SetExpanded(false), _ => HasResults);
        ToggleThemeCommand = new RelayCommand(_ => _theme.Toggle());
        SetLanguageCommand = new RelayCommand(p => SetLanguage(p));
        DownloadFfmpegCommand = new AsyncRelayCommand(DownloadFfmpegAsync, () => !IsDownloadingFfmpeg);

        RefreshFfmpegStatus();
    }

    // ---- Configuration ----------------------------------------------------

    public ObservableCollection<string> Folders { get; } = new();

    private bool _useExactContent = true;
    public bool UseExactContent { get => _useExactContent; set => SetProperty(ref _useExactContent, value); }

    private bool _useNameSimilarity;
    public bool UseNameSimilarity { get => _useNameSimilarity; set => SetProperty(ref _useNameSimilarity, value); }

    private bool _usePerceptualImage;
    public bool UsePerceptualImage { get => _usePerceptualImage; set => SetProperty(ref _usePerceptualImage, value); }

    private bool _usePerceptualVideo;
    public bool UsePerceptualVideo { get => _usePerceptualVideo; set => SetProperty(ref _usePerceptualVideo, value); }

    private bool _recursive = true;
    public bool Recursive { get => _recursive; set => SetProperty(ref _recursive, value); }

    private bool _includeHidden;
    public bool IncludeHidden { get => _includeHidden; set => SetProperty(ref _includeHidden, value); }

    private long _minSizeKb = 1;
    public long MinSizeKb { get => _minSizeKb; set => SetProperty(ref _minSizeKb, Math.Max(0, value)); }

    private long _maxSizeKb;
    public long MaxSizeKb { get => _maxSizeKb; set => SetProperty(ref _maxSizeKb, Math.Max(0, value)); }

    private string _includeExtensions = string.Empty;
    public string IncludeExtensions { get => _includeExtensions; set => SetProperty(ref _includeExtensions, value); }

    private string _excludeExtensions = string.Empty;
    public string ExcludeExtensions { get => _excludeExtensions; set => SetProperty(ref _excludeExtensions, value); }

    private double _nameThreshold = 0.85;
    public double NameThreshold { get => _nameThreshold; set => SetProperty(ref _nameThreshold, Math.Clamp(value, 0.5, 1.0)); }

    private int _perceptualThreshold = 8;
    public int PerceptualThreshold { get => _perceptualThreshold; set => SetProperty(ref _perceptualThreshold, Math.Clamp(value, 0, 32)); }

    private int _videoSamples = 12;
    public int VideoSamples { get => _videoSamples; set => SetProperty(ref _videoSamples, Math.Clamp(value, 2, 60)); }

    private int _videoIntroSkip = 8;
    public int VideoIntroSkip { get => _videoIntroSkip; set => SetProperty(ref _videoIntroSkip, Math.Clamp(value, 0, 40)); }

    private int _videoOutroSkip = 5;
    public int VideoOutroSkip { get => _videoOutroSkip; set => SetProperty(ref _videoOutroSkip, Math.Clamp(value, 0, 40)); }

    private bool _gentleResourceUsage = true;
    public bool GentleResourceUsage { get => _gentleResourceUsage; set => SetProperty(ref _gentleResourceUsage, value); }

    private string _ffmpegPath = string.Empty;
    public string FfmpegPath
    {
        get => _ffmpegPath;
        set { if (SetProperty(ref _ffmpegPath, value)) RefreshFfmpegStatus(); }
    }

    private bool _isDownloadingFfmpeg;
    public bool IsDownloadingFfmpeg
    {
        get => _isDownloadingFfmpeg;
        private set { if (SetProperty(ref _isDownloadingFfmpeg, value)) OnPropertyChanged(nameof(CanEditFfmpegPath)); }
    }

    public bool CanEditFfmpegPath => !_isDownloadingFfmpeg;

    private double _ffmpegDownloadProgress;
    public double FfmpegDownloadProgress { get => _ffmpegDownloadProgress; private set => SetProperty(ref _ffmpegDownloadProgress, value); }

    private string _ffmpegStatus = string.Empty;
    public string FfmpegStatus { get => _ffmpegStatus; private set => SetProperty(ref _ffmpegStatus, value); }

    private bool _ffmpegReady;
    public bool FfmpegReady { get => _ffmpegReady; private set => SetProperty(ref _ffmpegReady, value); }

    public IReadOnlyList<KeepRuleOption> KeepRules => KeepRuleOption.All;

    private KeepRuleOption _selectedKeepRule = KeepRuleOption.All[0];
    public KeepRuleOption SelectedKeepRule { get => _selectedKeepRule; set => SetProperty(ref _selectedKeepRule, value); }

    // ---- Progress / status ------------------------------------------------

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set { if (SetProperty(ref _isScanning, value)) OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isScanning;

    private string _statusText = Localization.Localization.Instance["Status.Idle"];
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private double _progressValue;
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }

    private bool _isIndeterminate;
    public bool IsIndeterminate { get => _isIndeterminate; private set => SetProperty(ref _isIndeterminate, value); }

    private string? _currentFile;
    public string? CurrentFile { get => _currentFile; private set => SetProperty(ref _currentFile, value); }

    // ---- Results ----------------------------------------------------------

    public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    private bool _hasScanned;
    public bool HasScanned { get => _hasScanned; private set => SetProperty(ref _hasScanned, value); }

    public bool HasResults => Groups.Count > 0;

    private int _filesScanned;
    public int FilesScanned { get => _filesScanned; private set => SetProperty(ref _filesScanned, value); }

    private int _redundantCount;
    public int RedundantCount { get => _redundantCount; private set => SetProperty(ref _redundantCount, value); }

    private string _reclaimableDisplay = "0 B";
    public string ReclaimableDisplay { get => _reclaimableDisplay; private set => SetProperty(ref _reclaimableDisplay, value); }

    private string _elapsedDisplay = "-";
    public string ElapsedDisplay { get => _elapsedDisplay; private set => SetProperty(ref _elapsedDisplay, value); }

    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        private set { if (SetProperty(ref _selectedCount, value)) OnPropertyChanged(nameof(SelectedSizeDisplay)); }
    }

    public string SelectedSizeDisplay => ByteSize.Humanize(SelectedBytes());

    public int GroupCount => Groups.Count;

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set { if (SetProperty(ref _filterText, value)) ApplyFilter(); }
    }

    // ---- Commands ---------------------------------------------------------

    public RelayCommand AddFolderCommand { get; }
    public RelayCommand RemoveFolderCommand { get; }
    public AsyncRelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand AutoSelectCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand RecycleCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand MoveCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ExportJsonCommand { get; }
    public RelayCommand OpenLocationCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand CopyPathCommand { get; }
    public RelayCommand CopyFolderCommand { get; }
    public RelayCommand SelectGroupRedundantCommand { get; }
    public RelayCommand ClearGroupSelectionCommand { get; }
    public RelayCommand ExpandAllCommand { get; }
    public RelayCommand CollapseAllCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand SetLanguageCommand { get; }
    public AsyncRelayCommand DownloadFfmpegCommand { get; }

    // ---- Folder management ------------------------------------------------

    public void AddFolders()
    {
        foreach (var folder in _dialogs.PickFolders())
            AddFolder(folder);
    }

    public void AddFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        if (!Folders.Contains(path, StringComparer.OrdinalIgnoreCase))
            Folders.Add(path);
    }

    private void RemoveFolder(string? path)
    {
        if (path is not null) Folders.Remove(path);
    }

    // ---- Scan -------------------------------------------------------------

    private async Task ScanAsync()
    {
        if (Folders.Count == 0)
        {
            _dialogs.Warn(L("Msg.NeedFolder"), L("Common.Warning"));
            return;
        }

        var methods = BuildMethods();
        if (methods == DetectionMethod.None)
        {
            _dialogs.Warn(L("Msg.NeedMethod"), L("Common.Warning"));
            return;
        }

        _cts = new CancellationTokenSource();
        IsScanning = true;
        HasScanned = true;
        Groups.Clear();
        Warnings.Clear();
        OnResultsChanged();

        var options = BuildOptions(methods);
        var progress = new Progress<ScanProgress>(OnProgress);

        try
        {
            var engine = new DuplicateScanEngine();
            var result = await engine.ScanAsync(options, progress, _cts.Token);
            ApplyResult(result);
            StatusText = L("Status.Completed");
        }
        catch (OperationCanceledException)
        {
            StatusText = L("Status.Cancelled");
        }
        catch (Exception ex)
        {
            StatusText = L("Status.Faulted");
            _dialogs.Warn(ex.Message, L("Status.Faulted"));
        }
        finally
        {
            IsScanning = false;
            IsIndeterminate = false;
            ProgressValue = 0;
            CurrentFile = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private DetectionMethod BuildMethods()
    {
        var m = DetectionMethod.None;
        if (UseExactContent) m |= DetectionMethod.ExactContent;
        if (UseNameSimilarity) m |= DetectionMethod.NameSimilarity;
        if (UsePerceptualImage) m |= DetectionMethod.PerceptualImage;
        if (UsePerceptualVideo) m |= DetectionMethod.PerceptualVideo;
        return m;
    }

    private ScanOptions BuildOptions(DetectionMethod methods) => new()
    {
        RootFolders = Folders.ToList(),
        Methods = methods,
        Recursive = Recursive,
        IncludeHidden = IncludeHidden,
        MinFileSizeBytes = MinSizeKb * 1024,
        MaxFileSizeBytes = MaxSizeKb * 1024,
        IncludeExtensions = ParseExtensions(IncludeExtensions),
        ExcludeExtensions = ParseExtensions(ExcludeExtensions),
        NameSimilarityThreshold = NameThreshold,
        PerceptualThreshold = PerceptualThreshold,
        VideoFrameSamples = VideoSamples,
        VideoIntroSkipPercent = VideoIntroSkip,
        VideoOutroSkipPercent = VideoOutroSkip,
        GentleResourceUsage = GentleResourceUsage,
        FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath,
    };

    private static HashSet<string> ParseExtensions(string raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var ext = part.Trim();
            if (!ext.StartsWith('.')) ext = "." + ext;
            set.Add(ext.ToLowerInvariant());
        }
        return set;
    }

    private void OnProgress(ScanProgress p)
    {
        StatusText = Localization.Localization.Instance[p.StatusKey];
        CurrentFile = p.CurrentFile;
        IsIndeterminate = p.Total <= 0 && p.Phase is not ScanPhase.Completed;
        ProgressValue = p.Percentage;
    }

    private void ApplyResult(ScanResult result)
    {
        Groups.Clear();
        var i = 0;
        foreach (var group in result.Groups)
        {
            var vm = new DuplicateGroupViewModel(++i, group);
            foreach (var file in vm.Files)
                file.PropertyChanged += OnFilePropertyChanged;
            Groups.Add(vm);
        }

        Warnings.Clear();
        foreach (var w in result.Warnings) Warnings.Add(w);

        FilesScanned = result.FilesScanned;
        RedundantCount = result.RedundantFileCount;
        ReclaimableDisplay = ByteSize.Humanize(result.ReclaimableBytes);
        ElapsedDisplay = $"{result.Elapsed.TotalSeconds:0.0}s";
        OnResultsChanged();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileItemViewModel.IsMarkedForRemoval))
            RefreshSelection();
    }

    // ---- Selection & actions ---------------------------------------------

    private void AutoSelect()
    {
        foreach (var group in Groups)
        {
            var redundant = DuplicateSelector.Redundant(group.Model, SelectedKeepRule.Rule);
            var redundantSet = new HashSet<string>(redundant.Select(f => f.FullPath), StringComparer.OrdinalIgnoreCase);
            foreach (var file in group.Files)
                file.IsMarkedForRemoval = redundantSet.Contains(file.FullPath);
        }
        RefreshSelection();
    }

    private void ClearSelection()
    {
        foreach (var file in AllFiles())
            file.IsMarkedForRemoval = false;
        RefreshSelection();
    }

    private void RefreshSelection() => SelectedCount = AllFiles().Count(f => f.IsMarkedForRemoval);

    private long SelectedBytes() => AllFiles().Where(f => f.IsMarkedForRemoval).Sum(f => f.Length);

    private void RecycleSelected()
    {
        var paths = MarkedPaths();
        if (paths.Count == 0) { _dialogs.Info(L("Msg.NothingSelected"), L("Common.Warning")); return; }
        if (!_dialogs.Confirm(Localization.Localization.Instance.Format("Msg.ConfirmRecycle", paths.Count), L("Common.Confirm")))
            return;

        var result = _fileActions.SendToRecycleBin(paths);
        AfterAction(result, paths);
    }

    private void DeleteSelected()
    {
        var paths = MarkedPaths();
        if (paths.Count == 0) { _dialogs.Info(L("Msg.NothingSelected"), L("Common.Warning")); return; }
        if (!_dialogs.Confirm(Localization.Localization.Instance.Format("Msg.ConfirmDelete", paths.Count), L("Common.Confirm")))
            return;

        var result = _fileActions.DeletePermanently(paths);
        AfterAction(result, paths);
    }

    private void MoveSelected()
    {
        var paths = MarkedPaths();
        if (paths.Count == 0) { _dialogs.Info(L("Msg.NothingSelected"), L("Common.Warning")); return; }

        var target = _dialogs.PickFolder(L("Action.MoveTo"));
        if (target is null) return;

        var result = _fileActions.MoveTo(paths, target);
        AfterAction(result, paths);
    }

    private void AfterAction(FileActionResult result, IReadOnlyList<string> attempted)
    {
        // Remove successfully-processed files from the view.
        var processed = new HashSet<string>(attempted, StringComparer.OrdinalIgnoreCase);
        var errored = new HashSet<string>(
            result.Errors.Select(e => e.Split(':', 2)[0]),
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups.ToList())
        {
            foreach (var file in group.Files.ToList())
            {
                if (processed.Contains(file.FullPath) && !errored.Contains(file.FullPath))
                {
                    file.PropertyChanged -= OnFilePropertyChanged;
                    group.Files.Remove(file);
                }
            }
            if (group.Files.Count < 2)
            {
                foreach (var f in group.Files) f.PropertyChanged -= OnFilePropertyChanged;
                Groups.Remove(group);
            }
        }

        RefreshSelection();
        OnResultsChanged();

        var msg = Localization.Localization.Instance.Format("Msg.ActionDone", result.Succeeded, ByteSize.Humanize(result.BytesFreed));
        if (result.HasErrors)
            msg += "\n" + Localization.Localization.Instance.Format("Msg.ActionErrors", result.Errors.Count);
        _dialogs.Info(msg, L("App.Title"));
    }

    private IReadOnlyList<string> MarkedPaths() =>
        AllFiles().Where(f => f.IsMarkedForRemoval).Select(f => f.FullPath).ToList();

    private IEnumerable<FileItemViewModel> AllFiles() => Groups.SelectMany(g => g.Files);

    private void OpenLocation(FileItemViewModel? file)
    {
        if (file is null || !File.Exists(file.FullPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.FullPath}\"") { UseShellExecute = true });
        }
        catch { /* ignore shell failures */ }
    }

    private void OpenFile(FileItemViewModel? file)
    {
        if (file is null || !File.Exists(file.FullPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
        }
        catch { /* ignore shell failures */ }
    }

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { System.Windows.Clipboard.SetText(text); } catch { /* clipboard may be busy */ }
    }

    private void SelectGroupRedundant(DuplicateGroupViewModel? group)
    {
        if (group is null) return;
        var redundant = new HashSet<string>(
            DuplicateSelector.Redundant(group.Model, SelectedKeepRule.Rule).Select(f => f.FullPath),
            StringComparer.OrdinalIgnoreCase);
        foreach (var file in group.Files)
            file.IsMarkedForRemoval = redundant.Contains(file.FullPath);
        RefreshSelection();
    }

    private void ClearGroupSelection(DuplicateGroupViewModel? group)
    {
        if (group is null) return;
        foreach (var file in group.Files) file.IsMarkedForRemoval = false;
        RefreshSelection();
    }

    private void ApplyFilter()
    {
        foreach (var group in Groups) group.ApplyFilter(_filterText);
    }

    private void SetExpanded(bool expanded)
    {
        foreach (var g in Groups) g.IsExpanded = expanded;
    }

    // ---- ffmpeg auto-provisioning ----------------------------------------

    private async Task DownloadFfmpegAsync()
    {
        IsDownloadingFfmpeg = true;
        FfmpegDownloadProgress = 0;
        FfmpegStatus = L("Ffmpeg.Downloading");
        try
        {
            var progress = new Progress<double>(p => FfmpegDownloadProgress = p);
            var path = await _ffmpegInstaller.InstallAsync(progress, CancellationToken.None);
            FfmpegPath = path;
            FfmpegStatus = L("Ffmpeg.Done");
        }
        catch (Exception ex)
        {
            FfmpegStatus = L("Ffmpeg.Failed");
            _dialogs.Warn(ex.Message, L("Ffmpeg.Failed"));
        }
        finally
        {
            IsDownloadingFfmpeg = false;
            RefreshFfmpegStatus();
        }
    }

    private void RefreshFfmpegStatus()
    {
        // Adopt a previously downloaded copy if the user hasn't set an explicit path.
        if (string.IsNullOrWhiteSpace(_ffmpegPath) && _ffmpegInstaller.IsInstalled)
        {
            _ffmpegPath = _ffmpegInstaller.FfmpegExePath;
            OnPropertyChanged(nameof(FfmpegPath));
        }

        FfmpegReady = !string.IsNullOrWhiteSpace(_ffmpegPath) || _ffmpegInstaller.IsInstalled;
        if (!IsDownloadingFfmpeg)
            FfmpegStatus = FfmpegReady ? L("Ffmpeg.Ready") : L("Ffmpeg.Missing");
    }

    // ---- Settings persistence --------------------------------------------

    public void ApplySettings(AppSettings s)
    {
        UseExactContent = s.UseExactContent;
        UseNameSimilarity = s.UseNameSimilarity;
        UsePerceptualImage = s.UsePerceptualImage;
        UsePerceptualVideo = s.UsePerceptualVideo;
        Recursive = s.Recursive;
        IncludeHidden = s.IncludeHidden;
        MinSizeKb = s.MinSizeKb;
        MaxSizeKb = s.MaxSizeKb;
        IncludeExtensions = s.IncludeExtensions;
        ExcludeExtensions = s.ExcludeExtensions;
        NameThreshold = s.NameThreshold;
        PerceptualThreshold = s.PerceptualThreshold;
        VideoSamples = s.VideoSamples;
        VideoIntroSkip = s.VideoIntroSkipPercent;
        VideoOutroSkip = s.VideoOutroSkipPercent;
        GentleResourceUsage = s.GentleResourceUsage;
        FfmpegPath = s.FfmpegPath;
        SelectedKeepRule = KeepRuleOption.All.FirstOrDefault(k => k.Rule == s.KeepRule) ?? KeepRuleOption.All[0];

        Folders.Clear();
        foreach (var folder in s.Folders)
            AddFolder(folder);
    }

    public void CaptureSettings(AppSettings s)
    {
        s.UseExactContent = UseExactContent;
        s.UseNameSimilarity = UseNameSimilarity;
        s.UsePerceptualImage = UsePerceptualImage;
        s.UsePerceptualVideo = UsePerceptualVideo;
        s.Recursive = Recursive;
        s.IncludeHidden = IncludeHidden;
        s.MinSizeKb = MinSizeKb;
        s.MaxSizeKb = MaxSizeKb;
        s.IncludeExtensions = IncludeExtensions;
        s.ExcludeExtensions = ExcludeExtensions;
        s.NameThreshold = NameThreshold;
        s.PerceptualThreshold = PerceptualThreshold;
        s.VideoSamples = VideoSamples;
        s.VideoIntroSkipPercent = VideoIntroSkip;
        s.VideoOutroSkipPercent = VideoOutroSkip;
        s.GentleResourceUsage = GentleResourceUsage;
        s.FfmpegPath = FfmpegPath;
        s.KeepRule = SelectedKeepRule.Rule;
        s.Folders = Folders.ToList();
    }

    // ---- Export -----------------------------------------------------------

    private async Task ExportCsvAsync()
    {
        var path = _dialogs.SaveFile("CSV (*.csv)|*.csv", ".csv", "duplicates");
        if (path is null) return;
        await ReportExporter.ExportCsvAsync(BuildExportResult(), path);
        _dialogs.Info(L("Msg.ExportDone"), L("App.Title"));
    }

    private async Task ExportJsonAsync()
    {
        var path = _dialogs.SaveFile("JSON (*.json)|*.json", ".json", "duplicates");
        if (path is null) return;
        await ReportExporter.ExportJsonAsync(BuildExportResult(), path);
        _dialogs.Info(L("Msg.ExportDone"), L("App.Title"));
    }

    private ScanResult BuildExportResult() => new()
    {
        Groups = Groups.Select(g => g.Model).ToList(),
        FilesScanned = FilesScanned,
        BytesScanned = 0,
        Elapsed = TimeSpan.Zero,
        Warnings = Warnings.ToList(),
    };

    // ---- Language ---------------------------------------------------------

    private void SetLanguage(object? param)
    {
        if (param is AppLanguage lang) Localization.Localization.Instance.Language = lang;
        else if (param is string s && Enum.TryParse<AppLanguage>(s, out var parsed))
            Localization.Localization.Instance.Language = parsed;

        // Status text is not bound through Loc, refresh it manually.
        StatusText = IsScanning ? StatusText : L("Status.Idle");
        OnPropertyChanged(nameof(KeepRules));
    }

    private void OnResultsChanged()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(GroupCount));
    }

    private static string L(string key) => Localization.Localization.Instance[key];
}
