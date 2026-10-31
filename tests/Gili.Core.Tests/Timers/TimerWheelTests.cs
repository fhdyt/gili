using Gili.Core.Timers;

namespace Gili.Core.Tests.Timers;

public class TimerWheelTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10, 0, 0, 10)]
    [InlineData(300, 0, 5, 0)]
    [InlineData(3725, 1, 2, 5)]
    [InlineData(86399, 23, 59, 59)]
    public void Splits_and_combines_hours_minutes_and_seconds(int total, int h, int m, int s)
    {
        Assert.Equal((h, m, s), TimerWheel.Split(total));
        Assert.Equal(total, TimerWheel.Combine(h, m, s));
    }

    [Fact]
    public void Split_clamps_to_the_wheels_range()
    {
        Assert.Equal((23, 59, 59), TimerWheel.Split(200_000));
        Assert.Equal((0, 0, 0), TimerWheel.Split(-5));
    }

    [Theory]
    [InlineData(0, 60, 0)]
    [InlineData(60, 60, 0)]
    [InlineData(-1, 60, 59)]
    [InlineData(-61, 60, 59)]
    [InlineData(25, 24, 1)]
    public void Wrap_loops_like_the_ios_wheel(int value, int count, int expected) => Assert.Equal(expected, WheelColumn.Wrap(value, count));

    [Fact]
    public void Scroll_moves_the_settling_value_at_once_and_loops_past_the_end()
    {
        var column = new WheelColumn(60, 58);
        column.Scroll(3);
        Assert.Equal(1, column.Value);
        Assert.True(column.IsMoving);
        Settle(column);
        Assert.Equal(61, column.Position, 3);
        Assert.Equal(1, column.Value);
        Assert.False(column.IsMoving);
    }

    [Fact]
    public void SetValue_takes_the_short_way_round()
    {
        var column = new WheelColumn(60, 2);
        column.SetValue(58, animate: true);
        Settle(column);
        Assert.Equal(-2, column.Position, 3);
        Assert.Equal(58, column.Value);
    }

    [Fact]
    public void SetValue_without_animation_jumps()
    {
        var column = new WheelColumn(24, 0);
        column.SetValue(5, animate: false);
        Assert.Equal(5, column.Position);
        Assert.False(column.IsMoving);
    }

    [Fact]
    public void Drag_follows_the_pointer_then_flings_and_snaps_to_a_whole_row()
    {
        var column = new WheelColumn(60, 10);
        column.BeginDrag();
        column.DragBy(1.4);
        Assert.Equal(11.4, column.Position, 3);
        Assert.True(column.IsMoving);
        column.EndDrag(0);
        Assert.Equal(11, column.Value);
        Settle(column);
        Assert.Equal(11, column.Position, 3);

        column.BeginDrag();
        column.EndDrag(20);
        Assert.Equal(17, column.Value);
    }

    [Fact]
    public void A_hard_fling_is_capped()
    {
        var column = new WheelColumn(60, 0);
        column.BeginDrag();
        column.EndDrag(10_000);
        Assert.Equal(WheelColumn.MaxFling, column.Value);
    }

    [Fact]
    public void A_new_target_keeps_the_motion_so_it_can_be_interrupted()
    {
        var column = new WheelColumn(60, 0);
        column.Scroll(5);
        column.Step(0.05);
        var mid = column.Position;
        Assert.InRange(mid, 0.01, 4.99);
        column.Scroll(-5);
        column.Step(0.001);
        Assert.InRange(column.Position, mid - 0.1, mid + 0.1);
        Assert.Equal(0, column.Value);
    }

    [Fact]
    public void RowAt_reads_the_row_under_an_offset_from_the_middle()
    {
        var column = new WheelColumn(60, 0);
        Assert.Equal(0, column.RowAt(0.4));
        Assert.Equal(2, column.RowAt(1.6));
        Assert.Equal(-1, column.RowAt(-1.2));
    }

    private static void Settle(WheelColumn column)
    {
        for (var i = 0; i < 600 && column.IsMoving; i++) column.Step(1 / 60.0);
    }
}
