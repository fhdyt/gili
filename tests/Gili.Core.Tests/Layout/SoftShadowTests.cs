using Gili.Core.Layout;

namespace Gili.Core.Tests.Layout;

public class SoftShadowTests
{
    [Fact]
    public void Layers_span_the_blur_around_the_spread()
    {
        var layers = SoftShadow.Layers(new ShadowSpec(OffsetY: 22, Blur: 44, Spread: -14, Alpha: 0.7), steps: 10);
        Assert.Equal(10, layers.Count);
        Assert.Equal(-14 - 22, layers[0].Inflate, 6);
        Assert.Equal(-14 + 22, layers[^1].Inflate, 6);
        Assert.All(layers, l => Assert.Equal(22, l.OffsetY));
        Assert.True(layers.Zip(layers.Skip(1)).All(p => p.First.Inflate < p.Second.Inflate));
    }

    [Fact]
    public void Nested_layers_add_up_to_the_shadow_alpha()
    {
        var layers = SoftShadow.Layers(new ShadowSpec(8, 18, -10, 0.5), steps: 8);
        var covered = 1 - layers.Aggregate(1.0, (left, l) => left * (1 - l.Alpha));
        Assert.Equal(0.5, covered, 6);
    }

    [Fact]
    public void Island_shadows_are_the_mockup_values() =>
        Assert.Equal([new ShadowSpec(22, 44, -14, 0.7), new ShadowSpec(8, 18, -10, 0.5)], SoftShadow.Island);
}
