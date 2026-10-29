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
/// A note card being dragged (PRD 6.3 "Satu benda dari awal sampai akhir"): from the moment it lifts it is this
/// window above the island and every other window, so the island's edge never clips it. It shows the card exactly,
/// at 1.03 around the grab point with the lifted shadow (mockup .fnote.proxy). Transparent, topmost, never activated
/// and click-through: the island keeps the mouse and stays active. One per drag; in 2h it becomes the floating note.
/// </summary>
public sealed class NoteDragWindow : Window
{
    private const double LiftScale = 1.03;

    private readonly double _margin;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Grid _lift;
    private IntPtr _hwnd;
    private Point _at;
    private Point _to;
    private EventHandler? _landing;
    private TaskCompletionSource? _landed;

    /// <param name="grab">Where the card is held, in its own coordinates; it grows around that point.</param>
    public NoteDragWindow(Note note, string edited, double width, double height, Point grab)
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
        // Room for the shadow below and the card grown by 3% toward any side.
        _margin = Math.Ceiling(24 + (LiftScale - 1) * Math.Max(width, height));
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        Width = width + 2 * _margin;
        Height = height + 2 * _margin;

        var card = new NoteCardView(note, edited) { Width = width, Height = height, IsHitTestVisible = false, Focusable = false, IsTabStop = false };
        // The lifted look (0 16 32 −10 black 75%, and a 1 px white 10% ring just outside the card) comes and goes with the lift.
        _lift = new Grid { Opacity = 0 };
        _lift.Children.Add(new SoftShadowView { Shadows = SoftShadow.LiftedNote, CornerRadius = 12 });
        _lift.Children.Add(new Border
        {
            Margin = new Thickness(-1),
            CornerRadius = new CornerRadius(13),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        });
        var body = new Grid
        {
            Width = width,
            Height = height,
            Margin = new Thickness(_margin),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = _scale,
            RenderTransformOrigin = new Point(width > 0 ? grab.X / width : 0.5, height > 0 ? grab.Y / height : 0.5),
        };
        body.Children.Add(_lift);
        body.Children.Add(card);
        Content = body;
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

    /// <summary>Puts the card's top-left (not the window's, which has room for the shadow) at this point in screen pixels.</summary>
    public void MoveTo(Point screenTopLeft)
    {
        _at = screenTopLeft;
        if (_hwnd == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        WindowStyles.MoveTo(_hwnd, (int)Math.Round(screenTopLeft.X - _margin * dpi.DpiScaleX), (int)Math.Round(screenTopLeft.Y - _margin * dpi.DpiScaleY));
    }

    /// <summary>Lifted: 1.03 around the grab point with the lifted shadow, in 220 ms on the out curve; at once without Windows animations.</summary>
    public void SetLifted(bool lifted)
    {
        var animated = SystemParameters.ClientAreaAnimation;
        Animate(_scale, ScaleTransform.ScaleXProperty, lifted ? LiftScale : 1, animated);
        Animate(_scale, ScaleTransform.ScaleYProperty, lifted ? LiftScale : 1, animated);
        Animate(_lift, OpacityProperty, lifted ? 1 : 0, animated);
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

    protected override void OnClosed(EventArgs e)
    {
        StopLanding();
        base.OnClosed(e);
    }

    private void StopLanding()
    {
        if (_landing is not null) CompositionTarget.Rendering -= _landing;
        _landing = null;
        _landed?.TrySetResult();
        _landed = null;
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
