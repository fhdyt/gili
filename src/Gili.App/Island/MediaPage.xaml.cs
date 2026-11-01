using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Controls;

namespace Gili.App.Island;

public partial class MediaPage : UserControl
{
    private static readonly SolidColorBrush LyricsOff = Frozen(Color.FromRgb(0x8E, 0x8E, 0x93));
    private static readonly SolidColorBrush LyricsOnBrush = Frozen(Color.FromRgb(0x30, 0xD1, 0x58));

    private readonly MediaService _media;
    private readonly Action _toggleLyrics;

    /// <param name="toggleLyrics">The Lyrics button flips Settings → Island → Show lyrics (F-ISL-26).</param>
    public MediaPage(MediaService media, Action toggleLyrics)
    {
        _media = media;
        _toggleLyrics = toggleLyrics;
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is IslandViewModel old)
            {
                old.PropertyChanged -= OnViewModelChanged;
                old.LyricRolled -= RollLyrics;
            }
            if (e.NewValue is not IslandViewModel vm) return;
            vm.PropertyChanged += OnViewModelChanged;
            vm.LyricRolled += RollLyrics;
            LyricsButton.Foreground = vm.LyricsOn ? LyricsOnBrush : LyricsOff;
        };
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IslandViewModel.LyricsOn) && sender is IslandViewModel vm)
            LyricsButton.Foreground = vm.LyricsOn ? LyricsOnBrush : LyricsOff;
    }

    private void OnLyrics(object sender, RoutedEventArgs e) => _toggleLyrics();

    /// <summary>A new line rolls the block up 10 px over 300 ms (mockup .lyr.roll); without animations it just changes.</summary>
    private void RollLyrics()
    {
        if (!SystemParameters.ClientAreaAnimation || !LyricsBlock.IsVisible) return;
        var move = (TranslateTransform)LyricsLines.RenderTransform;
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = BezierEase.Out });
        LyricsLines.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = BezierEase.Out });
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
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
