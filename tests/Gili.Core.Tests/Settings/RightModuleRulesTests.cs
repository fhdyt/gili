using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class RightModuleRulesTests
{
    private static readonly string[] Default = ["weather", "net", "cpu", "ram", "gpu", "disk"];

    [Fact]
    public void Defaults_hide_gpu_and_disk_in_the_prd_order()
    {
        var s = new AppSettings();
        Assert.Equal(RightModules.Gpu | RightModules.Disk, s.HiddenModules);
        Assert.Equal(Default, s.Order);
        Assert.Equal(Default, RightModuleRules.DefaultOrder);
    }

    [Fact]
    public void Order_repairs_duplicates_unknown_and_missing() =>
        Assert.Equal(["cpu", "weather", "net", "ram", "gpu", "disk"], RightModuleRules.Repair(["cpu", "cpu", "x", "weather"]));

    [Fact]
    public void Empty_or_missing_order_is_the_default()
    {
        Assert.Equal(Default, RightModuleRules.Repair(null));
        Assert.Equal(Default, new AppSettings { ModuleOrder = "" }.Normalize().Order);
        Assert.Equal("cpu,weather,net,ram,gpu,disk", new AppSettings { ModuleOrder = " cpu ,cpu,x,weather" }.Normalize().ModuleOrder);
    }

    [Fact]
    public void Move_down_and_up()
    {
        Assert.Equal(["net", "weather", "cpu", "ram", "gpu", "disk"], RightModuleRules.Move(Default, "weather", 1));
        Assert.Equal(["weather", "net", "cpu", "ram", "disk", "gpu"], RightModuleRules.Move(Default, "disk", -1));
    }

    [Fact]
    public void Move_at_the_edges_does_nothing()
    {
        Assert.Equal(Default, RightModuleRules.Move(Default, "weather", -1));
        Assert.Equal(Default, RightModuleRules.Move(Default, "disk", 1));
        Assert.Equal(Default, RightModuleRules.Move(Default, "wifi", 1));
    }

    [Fact]
    public void MoveTo_places_at_index()
    {
        Assert.Equal(["gpu", "weather", "net", "cpu", "ram", "disk"], RightModuleRules.MoveTo(Default, "gpu", 0));
        Assert.Equal(["net", "cpu", "ram", "gpu", "disk", "weather"], RightModuleRules.MoveTo(Default, "weather", 99));
    }

    [Theory]
    [InlineData("weather", RightModules.Weather)]
    [InlineData("net", RightModules.Network)]
    [InlineData("cpu", RightModules.Cpu)]
    [InlineData("ram", RightModules.Ram)]
    [InlineData("gpu", RightModules.Gpu)]
    [InlineData("disk", RightModules.Disk)]
    [InlineData("wifi", RightModules.Wifi)]
    [InlineData("bt", RightModules.Bluetooth)]
    [InlineData("vol", RightModules.Volume)]
    [InlineData("bri", RightModules.Brightness)]
    [InlineData("tray", RightModules.Tray)]
    [InlineData("clock", RightModules.Clock)]
    [InlineData("power", RightModules.None)]
    public void Bar_ids_map_to_their_flag(string id, RightModules flag) => Assert.Equal(flag, RightModuleRules.Of(id));
}
