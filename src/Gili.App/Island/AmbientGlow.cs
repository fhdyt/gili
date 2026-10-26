using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Gili.App.Controls;

namespace Gili.App.Island;

/// <summary>
/// The soft wash of the open page's colour behind the island's content (PRD 6.3 "Ambient glow", mockup <c>.glow</c>):
/// two blobs that drift to random points 10–16 s apart and breathe, never following the pointer. Everything runs at
/// 30 fps at most and only while the island is open; closed, nothing animates (PRD 8.1).
/// </summary>
public sealed class AmbientGlow : Canvas
{
    private const int FrameRate = 30;

    private readonly Blob[] _blobs;
    private readonly Random _random = new();
    private double _width = 560;
    private double _height = 256;
    private bool _running;

    public AmbientGlow()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Opacity = 0;
        // Blob 1 rests up and to the left; blob 2 down and to the right, half a breath later.
        _blobs = [new(-0.18, -0.30, TimeSpan.Zero), new(0.45, -0.20, TimeSpan.FromSeconds(-4.5))];
        foreach (var blob in _blobs) Children.Add(blob.Shape);
        SizeChanged += (_, _) => Place();
    }

    /// <summary>Cross-fades to the page's colours over 600 ms; <paramref name="dim"/> halves them (music paused on Media).</summary>
    public void SetColors(Color first, Color second, bool dim)
    {
        var same = first == second;
        var k = dim ? 0.5 : 1;
        _blobs[0].Fade(WithAlpha(first, 0.55 * k));
        _blobs[1].Fade(WithAlpha(second, (same ? 0.32 : 0.45) * k));
    }

    /// <summary>Shows the glow (400 ms) and lets it wander when <paramref name="moving"/>; the island's target size sets the reach.</summary>
    public void Show(double width, double height, bool moving)
    {
        _width = width;
        _height = height;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(400)));
        if (moving == _running) return;
        _running = moving;
        foreach (var blob in _blobs)
        {
            if (moving)
            {
                blob.Breathe();
                Leg(blob);
            }
            else
            {
                blob.Hold();
            }
        }
    }

    /// <summary>The island closed: fade out, stop, and return to the rest position for the next opening.</summary>
    public void Hide()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)));
        _running = false;
        foreach (var blob in _blobs) blob.Rest();
    }

    /// <summary>One drift: a random point up to 25% of the width and 30% of the height from rest, over 10–16 s.</summary>
    private void Leg(Blob blob)
    {
        if (!_running) return;
        var seconds = 10 + _random.NextDouble() * 6;
        var x = (_random.NextDouble() * 2 - 1) * _width * 0.25;
        var y = (_random.NextDouble() * 2 - 1) * _height * 0.3;
        blob.DriftTo(x, y, TimeSpan.FromSeconds(seconds), () => Leg(blob));
    }

    private void Place()
    {
        foreach (var blob in _blobs) blob.Place(ActualWidth, ActualHeight);
    }

    private static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B);

    /// <summary>A soft radial blob 75% of the island wide and 190% tall, placed relative to the island's size.</summary>
    private sealed class Blob
    {
        private readonly double _left;
        private readonly double _top;
        private readonly TimeSpan _breathOffset;
        private readonly GradientStop _inner = new(Colors.Transparent, 0);
        private readonly GradientStop _outer = new(Colors.Transparent, 1);
        private readonly ScaleTransform _breath = new(1, 1);
        private readonly TranslateTransform _drift = new();
        private DoubleAnimation? _legX;

        public Blob(double left, double top, TimeSpan breathOffset)
        {
            _left = left;
            _top = top;
            _breathOffset = breathOffset;
            Shape = new Ellipse
            {
                Fill = new RadialGradientBrush { GradientStops = { _inner, new GradientStop(Colors.Transparent, 0.5), _outer } },
                RenderTransform = new TransformGroup { Children = { _breath, _drift } },
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            Fade(Colors.Transparent);
        }

        public Ellipse Shape { get; }

        public void Place(double width, double height)
        {
            Shape.Width = width * 0.75;
            Shape.Height = height * 1.9;
            SetLeft(Shape, width * _left);
            SetTop(Shape, height * _top);
        }

        /// <summary>The colour fades out toward the edge; the middle stop keeps it soft without a blur effect, which the
        /// layered window would redraw on the CPU every frame.</summary>
        public void Fade(Color color)
        {
            var middle = (GradientStop)((RadialGradientBrush)Shape.Fill).GradientStops[1];
            Animate(_inner, color);
            Animate(middle, Color.FromArgb((byte)(color.A * 0.45), color.R, color.G, color.B));
            Animate(_outer, Color.FromArgb(0, color.R, color.G, color.B));
        }

        private static void Animate(GradientStop stop, Color color) =>
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(600)) { EasingFunction = new SineEase() });

        /// <summary>Scale 0.92 ↔ 1.08 over 9 s each way.</summary>
        public void Breathe()
        {
            var breath = new DoubleAnimation(0.92, 1.08, TimeSpan.FromSeconds(9))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = _breathOffset,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Timeline.SetDesiredFrameRate(breath, FrameRate);
            _breath.BeginAnimation(ScaleTransform.ScaleXProperty, breath);
            _breath.BeginAnimation(ScaleTransform.ScaleYProperty, breath);
        }

        public void DriftTo(double x, double y, TimeSpan duration, Action next)
        {
            _legX = new DoubleAnimation(x, duration) { EasingFunction = BezierEase.Glow };
            var legY = new DoubleAnimation(y, duration) { EasingFunction = BezierEase.Glow };
            Timeline.SetDesiredFrameRate(_legX, FrameRate);
            Timeline.SetDesiredFrameRate(legY, FrameRate);
            var leg = _legX;
            _legX.Completed += (_, _) =>
            {
                if (ReferenceEquals(leg, _legX)) next();
            };
            _drift.BeginAnimation(TranslateTransform.XProperty, _legX);
            _drift.BeginAnimation(TranslateTransform.YProperty, legY);
        }

        /// <summary>Stops where it is (music paused, or motion off): the current values stay on screen.</summary>
        public void Hold()
        {
            _legX = null;
            Freeze(_drift, TranslateTransform.XProperty);
            Freeze(_drift, TranslateTransform.YProperty);
            Freeze(_breath, ScaleTransform.ScaleXProperty);
            Freeze(_breath, ScaleTransform.ScaleYProperty);
        }

        public void Rest()
        {
            Hold();
            _drift.X = _drift.Y = 0;
        }

        private static void Freeze(Animatable target, DependencyProperty property)
        {
            var value = target.GetValue(property);
            target.BeginAnimation(property, null);
            target.SetValue(property, value);
        }
    }
}
