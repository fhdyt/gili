using Gili.Core.Layout;

namespace Gili.Core.Tests.Layout;

public class WheelScrollTests
{
    [Fact]
    public void Up_scrolls_until_the_top()
    {
        Assert.True(WheelScroll.CanScroll(40, 200, 120));
        Assert.False(WheelScroll.CanScroll(0, 200, 120));
    }

    [Fact]
    public void Down_scrolls_until_the_bottom()
    {
        Assert.True(WheelScroll.CanScroll(0, 200, -120));
        Assert.True(WheelScroll.CanScroll(199, 200, -120));
        Assert.False(WheelScroll.CanScroll(200, 200, -120));
    }

    [Fact]
    public void Nothing_to_scroll_lets_the_wheel_through()
    {
        Assert.False(WheelScroll.CanScroll(0, 0, 120));
        Assert.False(WheelScroll.CanScroll(0, 0, -120));
        Assert.False(WheelScroll.CanScroll(40, 200, 0));
        // Less than half a pixel left is the end.
        Assert.False(WheelScroll.CanScroll(0.3, 200, 120));
        Assert.False(WheelScroll.CanScroll(199.7, 200, -120));
    }
}
