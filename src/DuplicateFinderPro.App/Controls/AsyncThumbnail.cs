using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DuplicateFinderPro.App.Services;

namespace DuplicateFinderPro.App.Controls;

/// <summary>
/// An <see cref="Image"/> that loads its thumbnail asynchronously from
/// <see cref="MediaPreviewService"/> based on a file path + extension. Safe for
/// virtualized lists: when the container is recycled (path changes) any in-flight
/// load is cancelled. Shows nothing until the bitmap is ready.
/// </summary>
public sealed class AsyncThumbnail : Image
{
    private CancellationTokenSource? _cts;

    public AsyncThumbnail()
    {
        Stretch = Stretch.UniformToFill;
        Unloaded += (_, _) => _cts?.Cancel();
    }

    public static readonly DependencyProperty MediaPathProperty = DependencyProperty.Register(
        nameof(MediaPath), typeof(string), typeof(AsyncThumbnail),
        new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty MediaExtensionProperty = DependencyProperty.Register(
        nameof(MediaExtension), typeof(string), typeof(AsyncThumbnail),
        new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(
        nameof(DecodeWidth), typeof(int), typeof(AsyncThumbnail),
        new PropertyMetadata(96));

    public string? MediaPath
    {
        get => (string?)GetValue(MediaPathProperty);
        set => SetValue(MediaPathProperty, value);
    }

    public string? MediaExtension
    {
        get => (string?)GetValue(MediaExtensionProperty);
        set => SetValue(MediaExtensionProperty, value);
    }

    public int DecodeWidth
    {
        get => (int)GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AsyncThumbnail)d).Reload();

    private async void Reload()
    {
        _cts?.Cancel();
        Source = null;

        var path = MediaPath;
        var ext = MediaExtension;
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(ext)) return;

        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            var img = await MediaPreviewService.Instance.GetThumbnailAsync(path, ext, DecodeWidth, cts.Token);
            if (!cts.IsCancellationRequested)
                Source = img;
        }
        catch
        {
            // best-effort preview
        }
    }
}
