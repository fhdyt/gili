using System.Windows;
using System.Windows.Media.Animation;
using Gili.Core.Motion;

namespace Gili.App.Controls;

/// <summary>A WPF easing function from a CSS cubic-bezier, so animations ease exactly like the mockup.</summary>
public sealed class BezierEase : IEasingFunction
{
    public static readonly BezierEase Out = new(CubicBezier.Out);
    public static readonly BezierEase Expression = new(CubicBezier.Expression);
    public static readonly BezierEase Glow = new(CubicBezier.Glow);

    /// <summary>The mockup's <c>--pop</c> curve, which overshoots a little: switches in Settings.</summary>
    public static readonly BezierEase Pop = new(CubicBezier.Pop);

    /// <summary>CSS <c>ease-in</c> and <c>ease-out</c>, used by Gili's blink.</summary>
    public static readonly BezierEase In = new(new CubicBezier(0.42, 0, 1, 1));
    public static readonly BezierEase OutSoft = new(new CubicBezier(0, 0, 0.58, 1));

    private readonly CubicBezier _curve;

    public BezierEase(CubicBezier curve) => _curve = curve;

    public double Ease(double normalizedTime) => _curve.Ease(normalizedTime);

    public static DoubleAnimation To(double value, int milliseconds, BezierEase ease, int delay = 0) => new(value, TimeSpan.FromMilliseconds(milliseconds))
    {
        EasingFunction = ease,
        BeginTime = TimeSpan.FromMilliseconds(delay),
        FillBehavior = FillBehavior.HoldEnd,
    };
}
