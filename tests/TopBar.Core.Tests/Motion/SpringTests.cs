using TopBar.Core.Motion;

namespace TopBar.Core.Tests.Motion;

public class SpringTests
{
    private const double Frame = 1.0 / 60;

    private static double Run(Spring spring, double seconds, Action<double>? onFrame = null)
    {
        for (var t = 0.0; t < seconds; t += Frame)
        {
            spring.Step(Frame);
            onFrame?.Invoke(spring.Value);
        }
        return spring.Value;
    }

    [Fact]
    public void Settles_exactly_on_the_target()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        Run(spring, 3);
        Assert.Equal(100, spring.Value);
        Assert.True(spring.IsAtRest);
    }

    [Fact]
    public void The_opening_spring_overshoots_a_little()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        var max = 0.0;
        Run(spring, 2, v => max = Math.Max(max, v));
        Assert.InRange(max, 100.1, 103);
    }

    [Fact]
    public void The_closing_spring_never_overshoots()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Close, Target = 100 };
        var max = 0.0;
        Run(spring, 2, v => max = Math.Max(max, v));
        Assert.True(max <= 100.0001, $"max was {max}");
    }

    [Fact]
    public void The_closing_spring_is_within_one_percent_after_half_a_second()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Close, Target = 100 };
        Assert.InRange(Run(spring, 0.5), 99, 100);
    }

    [Fact]
    public void Retargeting_keeps_the_current_value_and_velocity()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        Run(spring, 0.1);
        var (value, velocity) = (spring.Value, spring.Velocity);

        spring.Target = 50;

        Assert.Equal(value, spring.Value);
        Assert.Equal(velocity, spring.Velocity);
        Assert.Equal(50, Run(spring, 3));
    }

    [Fact]
    public void SnapTo_jumps_without_motion()
    {
        var spring = new Spring(0) { Target = 100 };
        spring.SnapTo(40);
        Assert.Equal(40, spring.Value);
        Assert.Equal(40, spring.Target);
        Assert.True(spring.IsAtRest);
    }

    [Fact]
    public void A_long_stalled_frame_does_not_fling_the_value()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        spring.Step(5);
        Assert.InRange(spring.Value, 0, 110);
    }
}
