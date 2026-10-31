using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Controls;
using Gili.Core.Layout;
using Gili.Core.Motion;
using Gili.Core.Notes;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// A note being dragged (PRD 6.3 "Satu benda dari awal sampai akhir"): from the moment it lifts it is this window
/// above the island and every other window, so the island's edge never clips it. It shows the card (or the open
/// editor) exactly, at 1.03 around the grab point with the lifted shadow (mockup .fnote.proxy), and keeps that size
/// wherever it is held. Past the island's edge only its shadow deepens; let go outside, it grows into the floating
/// note's 260 × 220 around the grab point while its content cross-fades to the window's, and the floating note's own
/// window then takes its place. Transparent, topmost, never activated and click-through: the island keeps the mouse.
/// The window is sized from the start to hold both the card and the grown note, so growing never resizes it.
/// </summary>
public sealed class NoteDragWindow : Window
{
    private const double LiftScale = 1.03;

    private readonly double _width;
    private readonly double _height;
    private readonly double _fromRadius;
    private readonly double _liftScale;
    private readonly Point _fraction;
    // Where the card's top-left sits in the window, and where the grown note will (both in DIPs).
    private readonly Point _origin;
    private readonly NoteRect _grown;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Grid _body;
    private readonly Grid _lift;
    private readonly Grid _torn;
    private readonly Border _fill;
    private readonly FrameworkElement _source;
    private readonly FrameworkElement _look;
    private readonly RectangleGeometry _clip = new();
    private IntPtr _hwnd;
    private Point _at;
    private Point _to;
    private EventHandler? _landing;
    private TaskCompletionSource? _landed;
    private EventHandler? _growing;
    private TaskCompletionSource? _growTask;

    /// <param name="source">What is dragged, laid out at <paramref name="width"/> × <paramref name="height"/>: the card or the editor's text.</param>
    /// <param name="grab">Where it is held, in its own coordinates; it grows around that point.</param>
    /// <param name="radius">Its corners: 12 for a card, 16 for the editor.</param>
    public NoteDragWindow(Note note, FrameworkElement source, double width, double height, Point grab, double radius)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        UseLayoutRounding = true;
        Title = "Gili Note";
        // The island's text settings, which the card inherits there.
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 12;
        Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
        _width = width;
        _height = height;
        _fromRadius = radius;
        // Only a card grows by 3% as it lifts; the editor is lifted as it is (mockup .fnote.proxy.lifted.fromcard).
        _liftScale = radius > NoteFloat.Corner ? 1 : LiftScale;
        _fraction = new Point(width > 0 ? grab.X / width : 0.5, height > 0 ? grab.Y / height : 0.5);
        // The grown note keeps the grab point where it is (NoteFloat.Released), so its place next to the card is fixed.
        _grown = new NoteRect(_fraction.X * (width - NoteFloat.Width), _fraction.Y * (height - NoteFloat.Height), NoteFloat.Width, NoteFloat.Height);
        var left = Math.Min(0, _grown.X);
        var top = Math.Min(0, _grown.Y);
        // Room for the deepest shadow (42 px below) and the card grown by 3% toward any side.
        var margin = Math.Ceiling(48 + (LiftScale - 1) * Math.Max(width, height));
        _origin = new Point(margin - left, margin - top);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        Width = Math.Max(width, _grown.Right) - left + 2 * margin;
        Height = Math.Max(height, _grown.Bottom) - top + 2 * margin;

        // The lifted look (0 16 32 −10 black 75%, a 1 px white 10% ring just outside) comes and goes with the lift;
        // past the island's edge it deepens to 0 28 56 −14 black 80% with a 14% ring (mockup .fnote.proxy.torn).
        _lift = Shadow(SoftShadow.LiftedNote, 0x1A, radius);
        _lift.Opacity = 0;
        _torn = Shadow(SoftShadow.HeldNote, 0x24, radius);
        _torn.Opacity = 0;
        var rgb = NoteStyle.Colors[NoteStyle.ClampColor(note.Color)].Rgb;
        _fill = new Border { Background = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)) };
        _source = source;
        source.Width = width;
        source.Height = height;
        source.HorizontalAlignment = HorizontalAlignment.Left;
        source.VerticalAlignment = VerticalAlignment.Top;
        source.IsHitTestVisible = false;
        _look = Look(note);
        _look.Opacity = 0;
        var content = new Grid { Clip = _clip };
        content.Children.Add(_fill);
        content.Children.Add(source);
        content.Children.Add(_look);
        _body = new Grid
        {
            RenderTransform = _scale,
            RenderTransformOrigin = _fraction,
        };
        _body.Children.Add(_lift);
        _body.Children.Add(_torn);
        _body.Children.Add(content);
        var canvas = new Canvas();
        canvas.Children.Add(_body);
        Content = canvas;
        Frame(width, height, radius);
    }

    /// <summary>The note the card grows into once let go outside, in screen DIPs, before it is moved inside the work area.</summary>
    public NoteRect Grown
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return _grown with { X = _at.X / dpi.DpiScaleX + _grown.X, Y = _at.Y / dpi.DpiScaleY + _grown.Y };
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeClickThrough(_hwnd);
    }

    /// <summary>Shows the card with its top-left at this point in screen pixels, under the card it stands for.</summary>
    public void ShowAt(Point screenTopLeft)
    {
        new WindowInteropHelper(this).EnsureHandle();
        MoveTo(screenTopLeft);
        Show();
        MoveTo(screenTopLeft);
    }

    /// <summary>Puts the card's top-left (not the window's, which has room for the shadow and the grown note) at this point in screen pixels.</summary>
    public void MoveTo(Point screenTopLeft)
    {
        _at = screenTopLeft;
        if (_hwnd == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        WindowStyles.MoveTo(_hwnd, (int)Math.Round(screenTopLeft.X - _origin.X * dpi.DpiScaleX), (int)Math.Round(screenTopLeft.Y - _origin.Y * dpi.DpiScaleY));
    }

    /// <summary>Lifted: 1.03 around the grab point with the lifted shadow, in 220 ms on the out curve; at once without Windows animations.</summary>
    public void SetLifted(bool lifted)
    {
        var animated = SystemParameters.ClientAreaAnimation;
        var scale = lifted ? _liftScale : 1;
        Animate(_scale, ScaleTransform.ScaleXProperty, scale, animated);
        Animate(_scale, ScaleTransform.ScaleYProperty, scale, animated);
        Animate(_lift, OpacityProperty, lifted ? 1 : 0, animated);
        if (!lifted) Animate(_torn, OpacityProperty, 0, animated);
    }

    /// <summary>Past the island's edge the shadow deepens in 220 ms; back inside it is the lifted one again. The size never changes.</summary>
    public void SetTorn(bool torn)
    {
        var animated = SystemParameters.ClientAreaAnimation;
        Animate(_torn, OpacityProperty, torn ? 1 : 0, animated);
        Animate(_lift, OpacityProperty, torn ? 0 : 1, animated);
    }

    /// <summary>
    /// Lands in the slot (screen pixels): 340 ms on cubic-bezier(.34, 1.3, .64, 1), a little past it and back, while
    /// the lift settles. Without Windows animations it is there at once.
    /// </summary>
    public Task LandAsync(Rect screenSlot)
    {
        SetLifted(false);
        var from = _at;
        var to = _to = screenSlot.TopLeft;
        if (!SystemParameters.ClientAreaAnimation || _hwnd == IntPtr.Zero)
        {
            MoveTo(to);
            return Task.CompletedTask;
        }
        StopLanding();
        var clock = Stopwatch.StartNew();
        var landed = _landed = new TaskCompletionSource();
        _landing = (_, _) =>
        {
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / 340);
            var k = CubicBezier.Pop.Ease(t);
            MoveTo(new Point(from.X + (to.X - from.X) * k, from.Y + (to.Y - from.Y) * k));
            if (t >= 1) StopLanding();
        };
        CompositionTarget.Rendering += _landing;
        return landed.Task;
    }

    /// <summary>A landing still in flight ends in its slot at once.</summary>
    public void Land()
    {
        if (_landing is null) return;
        StopLanding();
        MoveTo(_to);
    }

    /// <summary>
    /// Let go outside the island (mockup landTear): the size springs to 260 × 220 with the open-island spring, worked out
    /// per frame with the grab point kept in place; the corners go to 12 px with it, the 1.03 lift settles to 1 in
    /// 220 ms, and the content cross-fades from the card (or editor) to the window's in 160 ms. Without Windows
    /// animations it is there at once.
    /// </summary>
    public Task GrowAsync()
    {
        StopGrowing();
        var grown = _growTask = new TaskCompletionSource();
        var animated = SystemParameters.ClientAreaAnimation;
        Animate(_scale, ScaleTransform.ScaleXProperty, 1, animated);
        Animate(_scale, ScaleTransform.ScaleYProperty, 1, animated);
        var fade = TimeSpan.FromMilliseconds(animated ? 160 : 100);
        _source.BeginAnimation(OpacityProperty, new DoubleAnimation(0, fade));
        _look.BeginAnimation(OpacityProperty, new DoubleAnimation(1, fade));
        if (!animated)
        {
            Frame(NoteFloat.Width, NoteFloat.Height, NoteFloat.Corner);
            grown.TrySetResult();
            return grown.Task;
        }
        var w = new Spring(_width) { Parameters = SpringParameters.Open, Target = NoteFloat.Width };
        var h = new Spring(_height) { Parameters = SpringParameters.Open, Target = NoteFloat.Height };
        var span = NoteFloat.Width - _width;
        var last = TimeSpan.Zero;
        _growing = (_, e) =>
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (now == last) return;
            var seconds = last == TimeSpan.Zero ? 1.0 / 60 : (now - last).TotalSeconds;
            last = now;
            w.Step(seconds);
            h.Step(seconds);
            var progress = Math.Abs(span) > 1 ? (w.Value - _width) / span : 1;
            Frame(w.Value, h.Value, NoteFloat.Radius(_fromRadius, progress));
            if (w.IsAtRest && h.IsAtRest) StopGrowing();
        };
        CompositionTarget.Rendering += _growing;
        return grown.Task;
    }

    /// <summary>A grow still in flight ends at the floating note's size at once.</summary>
    public void Grow()
    {
        if (_growing is null) return;
        StopGrowing();
        Frame(NoteFloat.Width, NoteFloat.Height, NoteFloat.Corner);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopLanding();
        StopGrowing();
        base.OnClosed(e);
    }

    /// <summary>The note's size and corners, with the grab point where it was: everything else is laid out from it.</summary>
    private void Frame(double width, double height, double radius)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        Canvas.SetLeft(_body, _origin.X + _fraction.X * (_width - width));
        Canvas.SetTop(_body, _origin.Y + _fraction.Y * (_height - height));
        _body.Width = width;
        _body.Height = height;
        _fill.CornerRadius = new CornerRadius(radius);
        _clip.Rect = new Rect(0, 0, width, height);
        _clip.RadiusX = radius;
        _clip.RadiusY = radius;
        foreach (var shadow in new[] { _lift, _torn })
        {
            ((SoftShadowView)shadow.Children[0]).CornerRadius = radius;
            ((Border)shadow.Children[1]).CornerRadius = new CornerRadius(radius + 1);
        }
    }

    /// <summary>The floating note's own look at its first size (mockup .fhd and textarea), drawn once to fade into.</summary>
    private static FrameworkElement Look(Note note)
    {
        var size = NoteStyle.Size(note.Size).Floating;
        var look = new Grid { Width = NoteFloat.Width, Height = NoteFloat.Height, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        look.Children.Add(new IconView
        {
            Glyph = (Geometry)Application.Current.FindResource("Icon.Grip"),
            Filled = true,
            Width = 16,
            Height = 16,
            Foreground = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)),
            LayoutTransform = new RotateTransform(90),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 0, 0),
        });
        var empty = note.Body.Length == 0;
        look.Children.Add(new TextBlock
        {
            Text = empty ? "Write something." : note.Body,
            Foreground = new SolidColorBrush(empty ? Color.FromArgb(0x73, 0xFF, 0xFF, 0xFF) : note.Color > 0 ? Color.FromArgb(0xEB, 0xFF, 0xFF, 0xFF) : Color.FromRgb(0xF5, 0xF5, 0xF7)),
            FontSize = size,
            LineHeight = size * 1.55,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(14, 28, 14, 12),
        });
        return look;
    }

    private static Grid Shadow(IReadOnlyList<ShadowSpec> shadows, byte ring, double radius)
    {
        var layer = new Grid();
        layer.Children.Add(new SoftShadowView { Shadows = shadows, CornerRadius = radius });
        layer.Children.Add(new Border
        {
            Margin = new Thickness(-1),
            CornerRadius = new CornerRadius(radius + 1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(ring, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        });
        return layer;
    }

    private void StopLanding()
    {
        if (_landing is not null) CompositionTarget.Rendering -= _landing;
        _landing = null;
        _landed?.TrySetResult();
        _landed = null;
    }

    private void StopGrowing()
    {
        if (_growing is not null) CompositionTarget.Rendering -= _growing;
        _growing = null;
        _growTask?.TrySetResult();
        _growTask = null;
    }

    private static void Animate<T>(T target, DependencyProperty property, double value, bool animated) where T : DependencyObject, IAnimatable
    {
        if (animated)
        {
            target.BeginAnimation(property, BezierEase.To(value, 220, BezierEase.Out));
            return;
        }
        target.BeginAnimation(property, null);
        target.SetValue(property, value);
    }
}
