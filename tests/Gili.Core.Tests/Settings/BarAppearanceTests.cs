using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class BarAppearanceTests
{
    [Fact]
    public void With_transparency_effects_the_chosen_glass_and_opacity_apply()
    {
        var look = BarAppearance.Resolve(new AppSettings { Glass = GlassLevel.Light, BarOpacity = 60 }, transparencyEffects: true);
        Assert.Equal(GlassLevel.Light, look.Glass);
        Assert.Equal(0.6, look.TintOpacity, 3);
    }

    [Fact]
    public void Without_transparency_effects_the_bar_is_solid()
    {
        var look = BarAppearance.Resolve(new AppSettings { Glass = GlassLevel.Strong, BarOpacity = 40 }, transparencyEffects: false);
        Assert.Equal(GlassLevel.Off, look.Glass);
        Assert.Equal(1.0, look.TintOpacity);
    }

    [Fact]
    public void The_tint_takes_the_bar_color() =>
        Assert.Equal("#15203D", BarAppearance.Resolve(new AppSettings { BarColor = "#15203D" }, transparencyEffects: true).Color);

    [Fact]
    public void The_backdrop_shows_fully_until_the_island_floats()
    {
        var s = new AppSettings();
        Assert.Equal(1.0, BarAppearance.Backdrop(s, islandFloating: false, desktopInFront: false));
        // Default 0 %: the backdrop goes away as before revision 14.
        Assert.Equal(0.0, BarAppearance.Backdrop(s, islandFloating: true, desktopInFront: false));
        Assert.Equal(0.35, BarAppearance.Backdrop(s with { OpenOpacity = 35 }, islandFloating: true, desktopInFront: false), 3);
    }

    [Fact]
    public void The_desktop_looks_like_the_open_island_while_same_on_the_desktop_is_on()
    {
        var s = new AppSettings { OpenOpacity = 50 };
        Assert.True(s.SameOnDesktop);
        Assert.Equal(0.5, BarAppearance.Backdrop(s, islandFloating: false, desktopInFront: true), 3);
        Assert.Equal(1.0, BarAppearance.Backdrop(s with { SameOnDesktop = false }, islandFloating: false, desktopInFront: true));
    }

    [Fact]
    public void Opacity_while_open_is_clamped_and_written_by_its_rows()
    {
        Assert.Equal(100, new AppSettings { OpenOpacity = 140 }.Normalize().OpenOpacity);
        Assert.Equal(0, new AppSettings { OpenOpacity = -5 }.Normalize().OpenOpacity);
        Assert.Equal(40, SettingsCatalog.Write(new AppSettings(), "openOp", 40).OpenOpacity);
        Assert.Equal((0, 100), SettingsCatalog.Range(new AppSettings(), "openOp"));
        Assert.False(SettingsCatalog.Write(new AppSettings(), "desktop", false).SameOnDesktop);
        Assert.Equal(true, SettingsCatalog.Read(new AppSettings(), "desktop"));
    }
}
