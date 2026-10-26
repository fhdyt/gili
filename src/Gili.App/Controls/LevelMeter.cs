using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Gili.App.Controls;

/// <summary>
/// Four bars that bounce while music plays and rest low while it is paused (PRD 6.3). Each bar has its own period so
/// they never move in step. Capped at 20 fps: it can run for hours, and the island is a layered window.
/// </summary>
public sealed class LevelMeter : StackPanel
{
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(LevelMeter), new PropertyMetadata(false, (d, _) => ((LevelMeter)d).Update()));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(LevelMeter),
        new PropertyMetadata(Brushes.White, (d, e) => ((LevelMeter)d).Recolor((Brush)e.NewValue)));

    private static readonly double[] Periods = [0.70, 0.62, 0.78, 0.55];
    private readonly ScaleTransform[] _scales;
    private bool _running;

    public LevelMeter()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        _scales = Periods.Select(_ => new ScaleTransform(1, 0.25)).ToArray();
        foreach (var scale in _scales)
        {
            Children.Add(new Rectangle
            {
                Width = 2.5,
                Height = 12,
                RadiusX = 1,
                RadiusY = 1,
                Margin = new Thickness(1, 0, 1, 0),
                Fill = BarBrush,
                RenderTransform = scale,
                RenderTransformOrigin = new Point(0.5, 0.5),
            });
        }
        IsVisibleChanged += (_, _) => Update();
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    private void Recolor(Brush brush)
    {
        foreach (var bar in Children.OfType<Rectangle>()) bar.Fill = brush;
    }

    private void Update()
    {
        var run = IsPlaying && IsVisible;
        if (run == _running) return;
        _running = run;
        for (var i = 0; i < _scales.Length; i++)
        {
            if (!run)
            {
                _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
                continue;
            }
            var bounce = new DoubleAnimation(0.25, 1, TimeSpan.FromSeconds(Periods[i]))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Timeline.SetDesiredFrameRate(bounce, 20);
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
        }
    }
}
