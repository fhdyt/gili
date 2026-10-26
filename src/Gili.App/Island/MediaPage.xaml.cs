using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Gili.App.Island;

public partial class MediaPage : UserControl
{
    private readonly MediaService _media;

    public MediaPage(MediaService media)
    {
        _media = media;
        InitializeComponent();
    }

    private void OnPlayPause(object sender, RoutedEventArgs e) => _ = _media.TogglePlayPauseAsync();

    private void OnPrevious(object sender, RoutedEventArgs e) => _ = _media.PreviousAsync();

    private void OnNext(object sender, RoutedEventArgs e) => _ = _media.NextAsync();

    /// <summary>Clicking the progress bar seeks there when the source allows it (PRD 6.3).</summary>
    private void OnSeek(object sender, MouseButtonEventArgs e)
    {
        if (_media.Current is not { CanSeek: true, HasTimeline: true } media) return;
        var bar = (FrameworkElement)sender;
        var fraction = Math.Clamp(e.GetPosition(bar).X / bar.ActualWidth, 0, 1);
        _ = _media.SeekAsync(media.Duration * fraction);
        e.Handled = true;
    }
}
