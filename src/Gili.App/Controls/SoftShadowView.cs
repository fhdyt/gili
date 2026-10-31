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

    private static readonly IReadOnlyList<(ShadowLayer Layer, Brush Brush)> IslandLayers = LayersOf(SoftShadow.Island, StepsFor(SoftShadow.Island));

    private IReadOnlyList<(ShadowLayer Layer, Brush Brush)> _layers = IslandLayers;
    private IReadOnlyList<ShadowSpec> _shadows = SoftShadow.Island;

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>The shadows drawn; the floating island's unless set.</summary>
    public IReadOnlyList<ShadowSpec> Shadows
    {
        get => _shadows;
        set
        {
            _shadows = value;
            _layers = LayersOf(value, StepsFor(value));
            InvalidateVisual();
        }
    }

    /// <summary>A wider blur needs more rectangles, or their edges show as bands (about one per 2 px of blur).</summary>
    private static int StepsFor(IReadOnlyList<ShadowSpec> shadows) => Math.Max(Steps, (int)Math.Ceiling(shadows.Max(s => s.Blur) / 2));

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var (layer, brush) in _layers)
        {
            var width = ActualWidth + 2 * layer.Inflate;
            var height = ActualHeight + 2 * layer.Inflate;
            if (width <= 0 || height <= 0) continue;
            var radius = Math.Clamp(CornerRadius + layer.Inflate, 0, Math.Min(width, height) / 2);
            dc.DrawRoundedRectangle(brush, null, new Rect(-layer.Inflate, -layer.Inflate + layer.OffsetY, width, height), radius, radius);
        }
    }

    private static IReadOnlyList<(ShadowLayer Layer, Brush Brush)> LayersOf(IReadOnlyList<ShadowSpec> shadows, int steps) =>
        [.. shadows.SelectMany(s => SoftShadow.Layers(s, steps)).Select(l => (l, Black(l.Alpha)))];

    private static Brush Black(double alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0));
        brush.Freeze();
        return brush;
    }
}
