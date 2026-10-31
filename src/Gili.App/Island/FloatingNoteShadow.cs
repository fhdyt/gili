using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Gili.App.Controls;
using Gili.Core.Layout;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// A floating note's shadow and its 1 px white ring, in a click-through window right below the note in the z-order
/// (plan 2h decision 2): a layered window takes every click on a pixel that is not fully clear, so a shadow drawn in
/// the note's own window would swallow clicks meant for the app beneath it.
/// </summary>
public sealed class FloatingNoteShadow : Window
{
    /// <summary>Room around the note for the held shadow: 14 px to the sides, 42 below, and the ring above.</summary>
    public static readonly Thickness Room = new(16, 4, 16, 46);

    private readonly Grid _rest;
    private readonly Grid _held;
    private IntPtr _hwnd;

    public FloatingNoteShadow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Title = "Gili Note Shadow";
        _rest = Layer(SoftShadow.FloatingNote, 0x1A);
        _held = Layer(SoftShadow.HeldNote, 0x24);
        _held.Opacity = 0;
        var body = new Grid { Margin = Room };
        body.Children.Add(_rest);
        body.Children.Add(_held);
        Content = body;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeClickThrough(_hwnd);
    }

    /// <summary>The deeper shadow while the note is held by its header (mockup .fnote.held), in 220 ms.</summary>
    public void SetHeld(bool held, bool animate = true)
    {
        var animated = animate && SystemParameters.ClientAreaAnimation;
        Fade(_held, held ? 1 : 0, animated);
        Fade(_rest, held ? 0 : 1, animated);
    }

    /// <summary>Right below the note window, around its bounds in screen pixels.</summary>
    public void Follow(IntPtr note, int left, int top, int width, int height)
    {
        if (_hwnd == IntPtr.Zero) new WindowInteropHelper(this).EnsureHandle();
        var dpi = VisualTreeHelper.GetDpi(this);
        int X(double dips) => (int)Math.Round(dips * dpi.DpiScaleX);
        int Y(double dips) => (int)Math.Round(dips * dpi.DpiScaleY);
        WindowStyles.PlaceBelow(_hwnd, note, left - X(Room.Left), top - Y(Room.Top),
            width + X(Room.Left + Room.Right), height + Y(Room.Top + Room.Bottom));
    }

    private static Grid Layer(IReadOnlyList<ShadowSpec> shadows, byte ring)
    {
        var layer = new Grid();
        layer.Children.Add(new SoftShadowView { Shadows = shadows, CornerRadius = 12 });
        layer.Children.Add(new Border
        {
            Margin = new Thickness(-1),
            CornerRadius = new CornerRadius(13),
            BorderBrush = new SolidColorBrush(Color.FromArgb(ring, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        });
        return layer;
    }

    private static void Fade(UIElement element, double to, bool animated)
    {
        if (animated)
        {
            element.BeginAnimation(OpacityProperty, BezierEase.To(to, 220, BezierEase.Out));
            return;
        }
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = to;
    }
}
