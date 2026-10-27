namespace Gili.Core.Layout;

/// <summary>A CSS-style box shadow: offset down, blur radius, spread (negative pulls it in), and opacity.</summary>
public sealed record ShadowSpec(double OffsetY, double Blur, double Spread, double Alpha);

/// <summary>One rounded rectangle of a soft shadow: grown by <paramref name="Inflate"/>, moved down, faintly black.</summary>
public sealed record ShadowLayer(double Inflate, double OffsetY, double Alpha);

/// <summary>
/// Soft shadows drawn as nested translucent rectangles instead of a blur effect. An effect in the island's layered
/// window is rendered again on every frame the glow moves, which held 20–40 MB of render surfaces (PRD 8.1).
/// </summary>
public static class SoftShadow
{
    /// <summary>The floating island's two shadows (mockup .island.float: 0 22 44 −14 black 70%, 0 8 18 −10 black 50%).</summary>
    public static readonly IReadOnlyList<ShadowSpec> Island = [new(22, 44, -14, 0.7), new(8, 18, -10, 0.5)];

    /// <summary>
    /// <paramref name="steps"/> rectangles spread evenly across the blur around the spread edge; where they all overlap
    /// they add up to the shadow's opacity, and toward the outside fewer overlap, so the edge fades.
    /// </summary>
    public static IReadOnlyList<ShadowLayer> Layers(ShadowSpec shadow, int steps)
    {
        var alpha = 1 - Math.Pow(1 - shadow.Alpha, 1.0 / steps);
        return [.. Enumerable.Range(0, steps).Select(i =>
            new ShadowLayer(shadow.Spread - shadow.Blur / 2 + shadow.Blur * i / (steps - 1), shadow.OffsetY, alpha))];
    }
}
