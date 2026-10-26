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
}
