using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.Core.Motion;

namespace Gili.App.Controls;

/// <summary>
/// How a bar element switched on or off in Settings comes and goes (F-BAR-12): one going away shrinks to 0.6 and fades
/// in 160 ms while its slot closes over 300 ms, the way a closed app leaves the strip; one coming back pops in. Its
/// neighbours slide when the order changes. Shared by the left side and the right modules.
/// </summary>
public sealed class SlotAnimation
{
    private static readonly BezierEase Pop = new(new CubicBezier(0.34, 1.3, 0.64, 1));
    private static readonly TimeSpan Slot = TimeSpan.FromMilliseconds(300);

    private readonly HashSet<FrameworkElement> _leaving = [];
    private readonly Dictionary<FrameworkElement, double> _arriving = [];

    /// <summary>Shown, or still shrinking away (which counts as gone, so turning it back on mid-way brings it back).</summary>
    public bool IsShown(FrameworkElement element) =>
        element.Visibility == Visibility.Visible && !_leaving.Contains(element);

    /// <summary>The width the element takes once settled: an arriving one is measured mid-way, so its full width is kept.</summary>
    public double SettledWidth(FrameworkElement element)
    {
        if (_arriving.TryGetValue(element, out var full)) return full + element.Margin.Left + element.Margin.Right;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width;
    }

    public void Show(FrameworkElement element, bool show, bool animate)
    {
        if (IsShown(element) == show) return;
        var (scale, _) = Transforms(element);
        _leaving.Remove(element);
        _arriving.Remove(element);
        element.BeginAnimation(FrameworkElement.WidthProperty, null);
        element.BeginAnimation(FrameworkElement.MinWidthProperty, null);
        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            return;
        }
        element.Visibility = Visibility.Visible;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var full = element.DesiredSize.Width - element.Margin.Left - element.Margin.Right;
        var slot = new DoubleAnimation(show ? 0 : full, show ? full : 0, Slot) { EasingFunction = BezierEase.Out };
        if (show) _arriving[element] = full;
        else _leaving.Add(element);
        slot.Completed += (_, _) =>
        {
            if (show ? !_arriving.Remove(element) : !_leaving.Remove(element)) return;
            if (!show) element.Visibility = Visibility.Collapsed;
            element.BeginAnimation(FrameworkElement.WidthProperty, null);
            element.BeginAnimation(FrameworkElement.MinWidthProperty, null);
        };
        // A module's minimum width would stop its slot from closing, so it shrinks along.
        element.BeginAnimation(FrameworkElement.MinWidthProperty, slot);
        element.BeginAnimation(FrameworkElement.WidthProperty, slot);
        var size = show
            ? new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = Pop }
            : new DoubleAnimation(0.6, TimeSpan.FromMilliseconds(160));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, size);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, size);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(show ? 200 : 160)));
    }

    /// <summary>An element that moved by <paramref name="dx"/> in the layout glides from where it was (300 ms).</summary>
    public static void Slide(FrameworkElement element, double dx)
    {
        if (Math.Abs(dx) < 0.5 || !SystemParameters.ClientAreaAnimation) return;
        var (_, move) = Transforms(element);
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-dx, 0, Slot) { EasingFunction = BezierEase.Out });
    }

    private static (ScaleTransform Scale, TranslateTransform Move) Transforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup { Children: [ScaleTransform scale, TranslateTransform move] }) return (scale, move);
        scale = new ScaleTransform(1, 1);
        move = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { scale, move } };
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return (scale, move);
    }
}
