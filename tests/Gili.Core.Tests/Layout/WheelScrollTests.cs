using Gili.Core.Layout;

namespace Gili.Core.Tests.Layout;

public class WheelScrollTests
{
    [Fact]
    public void A_list_that_scrolls_keeps_the_wheel_even_at_its_ends()
    {
        Assert.True(WheelScroll.KeepsWheel(200));
        Assert.True(WheelScroll.KeepsWheel(1));
    }

    [Fact]
    public void Nothing_to_scroll_lets_the_wheel_through()
    {
        Assert.False(WheelScroll.KeepsWheel(0));
        // Less than half a pixel to scroll is nothing to scroll.
        Assert.False(WheelScroll.KeepsWheel(0.3));
    }

    [Fact]
    public void A_notch_moves_the_lines_set_in_Windows()
    {
        // Three lines of 16 px each way, like a ScrollViewer; up is a positive delta.
        Assert.Equal(52, WheelScroll.Offset(100, 400, 120, 3, 150));
        Assert.Equal(148, WheelScroll.Offset(100, 400, -120, 3, 150));
        // A touchpad's small delta moves as far as a notch, as it does in a ScrollViewer.
        Assert.Equal(148, WheelScroll.Offset(100, 400, -8, 3, 150));
        // "One screen at a time" moves the visible height.
        Assert.Equal(250, WheelScroll.Offset(100, 400, -120, -1, 150));
        Assert.Equal(100, WheelScroll.Offset(100, 400, 0, 3, 150));
    }

    [Fact]
    public void The_offset_stays_within_the_list()
    {
        Assert.Equal(0, WheelScroll.Offset(20, 400, 120, 3, 150));
        Assert.Equal(400, WheelScroll.Offset(380, 400, -120, 3, 150));
        Assert.Equal(0, WheelScroll.Offset(0, 0, -120, 3, 150));
    }
}
