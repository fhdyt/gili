using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Controls;
using Gili.Core.Mascot;
using Gili.Core.Motion;

namespace Gili.App.Mascot;

/// <summary>
/// Gili's two white eyes in one form (PRD 6.3 "Maskot Gili"; mockup <c>.geyes</c>). The pair moves with the gaze; each
/// expression is a scale, a nudge and a corner radius on the eyes, so a blink (a scale on top) still plays over it.
/// </summary>
public sealed class GiliEyes : Grid
{
    public static readonly DependencyProperty FormProperty = DependencyProperty.Register(
        nameof(Form), typeof(GiliForm), typeof(GiliEyes), new PropertyMetadata(GiliForm.InIsland, (d, _) => ((GiliEyes)d).Resize()));

    public static readonly DependencyProperty IslandHeightProperty = DependencyProperty.Register(
        nameof(IslandHeight), typeof(int), typeof(GiliEyes), new PropertyMetadata(24, (d, _) => ((GiliEyes)d).Resize()));

    private static readonly Brush ZBrush = Frozen(Color.FromRgb(0x8E, 0x8E, 0x93));

    private readonly StackPanel _pair = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border[] _eyes = [new() { Background = Brushes.White }, new() { Background = Brushes.White }];
    private readonly TranslateTransform _gaze = new();
    private readonly TranslateTransform _nod = new();
    private readonly ScaleTransform _shape = new(1, 1);
    private readonly TranslateTransform _shift = new();
    private readonly ScaleTransform _blink = new(1, 1);
    private readonly TextBlock _z = new() { Text = "z", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = ZBrush, Opacity = 0 };
    private readonly TranslateTransform _zRise = new(0, 2);
    private GiliMood _mood = GiliMood.Neutral;
    private GiliEyeSize _size;

    public GiliEyes()
    {
        IsHitTestVisible = false;
        var eyeTransform = new TransformGroup();
        eyeTransform.Children.Add(_shape);
        eyeTransform.Children.Add(_blink);
        eyeTransform.Children.Add(_shift);
        foreach (var eye in _eyes)
        {
            eye.RenderTransform = eyeTransform;
            _pair.Children.Add(eye);
        }
        _z.RenderTransform = _zRise;
        _pair.Children.Add(new Canvas { Width = 0, Children = { _z } });
        var pairTransform = new TransformGroup();
        pairTransform.Children.Add(_gaze);
        pairTransform.Children.Add(_nod);
        _pair.RenderTransform = pairTransform;
        Children.Add(_pair);
        Resize();
    }

    public GiliForm Form
    {
        get => (GiliForm)GetValue(FormProperty);
        set => SetValue(FormProperty, value);
    }

    public int IslandHeight
    {
        get => (int)GetValue(IslandHeightProperty);
        set => SetValue(IslandHeightProperty, value);
    }

    /// <summary>The pair's width and height, for the gaze's reach.</summary>
    public Size PairSize => new(_size.Side * 2 + _size.Gap, _size.Side);

    /// <summary>Moves the pair for an aim from −1 to 1; redraws only when it moves at least 0.25 px (PRD 6.3 "Ringan").</summary>
    public void Look(double aimX, double aimY)
    {
        var pair = PairSize;
        var (x, y) = GiliRules.Offset(aimX, aimY, Form, ActualWidth + 18, ActualHeight, pair.Width, pair.Height);
        if (Math.Abs(x - _gaze.X) < 0.25 && Math.Abs(y - _gaze.Y) < 0.25) return;
        _gaze.X = x;
        _gaze.Y = y;
    }

    public void Blink(bool twice)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(200) };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.2, KeyTime.FromPercent(0.5)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(1)));
            _pair.BeginAnimation(OpacityProperty, fade);
            return;
        }
        // 230 ms: shut by 39% easing in, open easing out. Twice: 580 ms with the eyes open from 39.7% to 60.3%.
        var blink = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(twice ? 580 : 230) };
        if (twice)
        {
            blink.KeyFrames.Add(Key(0.1, 0.155, BezierEase.In));
            blink.KeyFrames.Add(Key(1, 0.397, BezierEase.OutSoft));
            blink.KeyFrames.Add(Key(1, 0.603, null));
            blink.KeyFrames.Add(Key(0.1, 0.759, BezierEase.In));
            blink.KeyFrames.Add(Key(1, 1, BezierEase.OutSoft));
        }
        else
        {
            blink.KeyFrames.Add(Key(0.1, 0.39, BezierEase.In));
            blink.KeyFrames.Add(Key(1, 1, BezierEase.OutSoft));
        }
        _blink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
    }

    /// <summary>Changes the eyes' shape for an expression (PRD 6.3 "Ekspresi Gili"; mockup <c>.x-*</c>).</summary>
    public void SetMood(GiliMood mood)
    {
        if (mood == _mood) return;
        _mood = mood;
        var animate = SystemParameters.ClientAreaAnimation;
        var duration = mood == GiliMood.Sleep ? 900 : 260;
        var (scaleX, scaleY, shiftY) = mood switch
        {
            GiliMood.Enjoy => (1.0, 0.5, 0.0),
            GiliMood.Wide => (1.3, 1.3, 0.0),
            GiliMood.Sleep => (1.0, 0.16, 1.0),
            GiliMood.Happy => (1.0, 0.42, 0.0),
            _ => (1.0, 1.0, 0.0),
        };
        foreach (var eye in _eyes)
        {
            // Enjoying music closes the top half, and happy eyes lose their bottom half: both shrink toward their
            // bottom edge. Happy eyes are arcs, round on top (mockup .x-happy: radius 50% 50% 1px 1px).
            eye.RenderTransformOrigin = mood is GiliMood.Enjoy or GiliMood.Happy ? new Point(0.5, 1) : new Point(0.5, 0.5);
            eye.CornerRadius = mood switch
            {
                GiliMood.Wide => new CornerRadius(_size.Side / 2),
                GiliMood.Happy => new CornerRadius(_size.Side / 2, _size.Side / 2, 1, 1),
                GiliMood.Sleep => new CornerRadius(2),
                _ => new CornerRadius(_size.Radius),
            };
        }
        Animate(_shape, ScaleTransform.ScaleXProperty, scaleX, duration, animate);
        Animate(_shape, ScaleTransform.ScaleYProperty, scaleY, duration, animate);
        Animate(_shift, TranslateTransform.YProperty, shiftY, duration, animate);
        Nod(mood == GiliMood.Enjoy && animate);
        var asleep = mood == GiliMood.Sleep && Form == GiliForm.InIsland;
        _z.BeginAnimation(OpacityProperty, animate
            ? new DoubleAnimation(asleep ? 1 : 0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(asleep ? 600 : 0) }
            : null);
        if (!animate) _z.Opacity = asleep ? 1 : 0;
        _zRise.BeginAnimation(TranslateTransform.YProperty, animate ? BezierEase.To(asleep ? 0 : 2, 900, BezierEase.Out, asleep ? 600 : 0) : null);
    }

    private static void Animate(Animatable target, DependencyProperty property, double value, int milliseconds, bool animate)
    {
        if (!animate)
        {
            target.BeginAnimation(property, null);
            target.SetValue(property, value);
            return;
        }
        target.BeginAnimation(property, BezierEase.To(value, milliseconds, BezierEase.Expression));
    }

    /// <summary>Music: a quick dip of 22% of the eye every 0.48 s, back up, then still until the next beat.</summary>
    private void Nod(bool on)
    {
        if (!on)
        {
            _nod.BeginAnimation(TranslateTransform.YProperty, null);
            _nod.Y = 0;
            return;
        }
        var nod = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(480), RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(nod, 30);
        nod.KeyFrames.Add(Key(_size.Side * 0.22, 0.25, new BezierEase(new CubicBezier(0.3, 0, 0.6, 1))));
        nod.KeyFrames.Add(Key(0, 0.6, new BezierEase(new CubicBezier(0.4, 0, 0.2, 1))));
        nod.KeyFrames.Add(Key(0, 1, null));
        _nod.BeginAnimation(TranslateTransform.YProperty, nod);
    }

    private static EasingDoubleKeyFrame Key(double value, double at, IEasingFunction? ease) =>
        new(value, KeyTime.FromPercent(at), ease);

    private void Resize()
    {
        _size = GiliRules.Eyes(Form, IslandHeight);
        for (var i = 0; i < _eyes.Length; i++)
        {
            _eyes[i].Width = _size.Side;
            _eyes[i].Height = _size.Side;
            _eyes[i].CornerRadius = new CornerRadius(_size.Radius);
            _eyes[i].Margin = new Thickness(i == 0 ? 0 : _size.Gap, 0, 0, 0);
        }
        // The z floats 3 px right of the eyes with its bottom at 60% of their height (mockup .gz); 8 px text is ~11 px tall.
        Canvas.SetLeft(_z, 3);
        Canvas.SetTop(_z, _size.Side * 0.4 - 11);
        var mood = _mood;
        _mood = (GiliMood)(-1);
        SetMood(mood);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
