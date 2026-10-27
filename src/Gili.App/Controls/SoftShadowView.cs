using System.Windows;
using System.Windows.Media;
using Gili.Core.Layout;

namespace Gili.App.Controls;

/// <summary>
/// The floating island's shadow, drawn as nested translucent rounded rectangles (SoftShadow) instead of
/// DropShadowEffects, which the layered window rendered again on every frame of the moving glow (PRD 8.1).
/// Sized and placed like the island it sits under.
/// </summary>
public sealed class SoftShadowView : FrameworkElement
{
    private const int Steps = 10;

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(SoftShadowView), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly IReadOnlyList<(ShadowLayer Layer, Brush Brush)> Layers =
        [.. SoftShadow.Island.SelectMany(s => SoftShadow.Layers(s, Steps)).Select(l => (l, Black(l.Alpha)))];

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var (layer, brush) in Layers)
        {
            var width = ActualWidth + 2 * layer.Inflate;
            var height = ActualHeight + 2 * layer.Inflate;
            if (width <= 0 || height <= 0) continue;
            var radius = Math.Clamp(CornerRadius + layer.Inflate, 0, Math.Min(width, height) / 2);
            dc.DrawRoundedRectangle(brush, null, new Rect(-layer.Inflate, -layer.Inflate + layer.OffsetY, width, height), radius, radius);
        }
    }

    private static Brush Black(double alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0));
        brush.Freeze();
        return brush;
    }
}
