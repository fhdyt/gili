using Gili.Core.Motion;

namespace Gili.Core.Tests.Motion;

public class CubicBezierTests
{
    [Fact]
    public void Ends_are_fixed()
    {
        Assert.Equal(0, CubicBezier.Out.Ease(0));
        Assert.Equal(1, CubicBezier.Out.Ease(1));
    }

    [Fact]
    public void Out_curve_is_fast_early() => Assert.InRange(CubicBezier.Out.Ease(0.25), 0.80, 0.85);

    [Fact]
    public void Linear_control_points_are_linear() => Assert.Equal(0.4, new CubicBezier(0, 0, 1, 1).Ease(0.4), 3);

    [Fact]
    public void Expression_curve_overshoots() => Assert.True(CubicBezier.Expression.Ease(0.6) > 1);
}
