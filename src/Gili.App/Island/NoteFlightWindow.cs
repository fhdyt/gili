using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.Core.Motion;
using Gili.Core.Notes;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// A floating note flying home (PRD 6.3 "Back to island", mockup returnFloat). A layered window redrawn at a new size
/// every frame falls behind and shows stale frames, so the flight is drawn in this click-through window, sized once
/// to hold both where the note was and where it goes, and only the shape inside it moves: the note's look fades in
/// 100 ms while it speeds toward the target on cubic-bezier(.55, 0, .75, .2) for 440 ms, its corners and fill going to
/// the target's, and fading out over the last quarter when it vanishes into the island.
/// </summary>
public sealed class NoteFlightWindow : Window
{
    private const double Room = 2;

    private readonly NoteRect _from;
    private readonly NoteRect _to;
    private readonly NoteRect _window;
    private readonly double _toRadius;
    private readonly Color _fromFill;
    private readonly Color _toFill;
    private readonly bool _vanish;
    private readonly SolidColorBrush _fill;
    private readonly Border _body;
    private readonly Image _look;
    private EventHandler? _flying;
    private TaskCompletionSource? _flown;

    /// <param name="look">The note as it was on screen, which fades out as it leaves.</param>
    public NoteFlightWindow(NoteRect from, NoteRect to, Color fromFill, Color toFill, double toRadius, bool vanish, ImageSource? look)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        UseLayoutRounding = false;
        Title = "Gili Note";
        WindowStartupLocation = WindowStartupLocation.Manual;
        _from = from;
        _to = to;
        _toRadius = toRadius;
        _fromFill = fromFill;
        _toFill = toFill;
        _vanish = vanish;
        var left = Math.Min(from.X, to.X) - Room;
        var top = Math.Min(from.Y, to.Y) - Room;
        _window = new NoteRect(left, top, Math.Max(from.Right, to.Right) + Room - left, Math.Max(from.Bottom, to.Bottom) + Room - top);
        Current = from;
        _fill = new SolidColorBrush(fromFill);
        _look = new Image { Source = look, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _body = new Border
        {
            Background = _fill,
            CornerRadius = new CornerRadius(NoteFloat.Corner),
            ClipToBounds = true,
            Child = _look,
        };
        var canvas = new Canvas();
        canvas.Children.Add(_body);
        Content = canvas;
        Frame(from, NoteFloat.Corner);
    }

    /// <summary>Where the note is now, in screen DIPs: Gili's eyes follow it.</summary>
    public NoteRect Current { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.MakeClickThrough(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Shows the note where it is now, placed once for the whole flight.</summary>
    public void ShowStill()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var dpi = VisualTreeHelper.GetDpi(this);
        var x = (int)Math.Round(_window.X * dpi.DpiScaleX);
        var y = (int)Math.Round(_window.Y * dpi.DpiScaleY);
        WindowStyles.Place(hwnd, x, y, (int)Math.Round(_window.Right * dpi.DpiScaleX) - x, (int)Math.Round(_window.Bottom * dpi.DpiScaleY) - y);
        Show();
    }

    public Task FlyAsync()
    {
        StopFlying();
        var flown = _flown = new TaskCompletionSource();
        _look.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(100)));
        var ease = new CubicBezier(0.55, 0, 0.75, 0.2);
        var clock = Stopwatch.StartNew();
        _flying = (_, _) =>
        {
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / 440);
            var k = ease.Ease(t);
            Frame(new NoteRect(
                _from.X + (_to.X - _from.X) * k,
                _from.Y + (_to.Y - _from.Y) * k,
                _from.Width + (_to.Width - _from.Width) * k,
                _from.Height + (_to.Height - _from.Height) * k), NoteFloat.Corner + (_toRadius - NoteFloat.Corner) * k);
            _fill.Color = Mix(_fromFill, _toFill, k);
            if (_vanish) _body.Opacity = t < 0.75 ? 1 : 1 - (t - 0.75) / 0.25;
            if (t >= 1) StopFlying();
        };
        CompositionTarget.Rendering += _flying;
        return flown.Task;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopFlying();
        base.OnClosed(e);
    }

    private void Frame(NoteRect rect, double radius)
    {
        Current = rect;
        Canvas.SetLeft(_body, rect.X - _window.X);
        Canvas.SetTop(_body, rect.Y - _window.Y);
        _body.Width = Math.Max(1, rect.Width);
        _body.Height = Math.Max(1, rect.Height);
        _body.CornerRadius = new CornerRadius(Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2));
    }

    private void StopFlying()
    {
        if (_flying is not null) CompositionTarget.Rendering -= _flying;
        _flying = null;
        _flown?.TrySetResult();
        _flown = null;
    }

    private static Color Mix(Color a, Color b, double k) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * k),
        (byte)Math.Round(a.R + (b.R - a.R) * k),
        (byte)Math.Round(a.G + (b.G - a.G) * k),
        (byte)Math.Round(a.B + (b.B - a.B) * k));
}
