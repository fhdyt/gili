namespace Gili.Core.Motion;

/// <summary>
/// A CSS cubic-bezier timing curve, so the app eases exactly like the mockup: x is time, y is progress, both 0 to 1.
/// </summary>
public sealed class CubicBezier(double x1, double y1, double x2, double y2)
{
    /// <summary>The mockup's "out" curve: names, the tab highlight, sliding pages and the open island's content.</summary>
    public static readonly CubicBezier Out = new(0.16, 1, 0.3, 1);

    /// <summary>Gili's eyes changing shape, with a small overshoot.</summary>
    public static readonly CubicBezier Expression = new(0.32, 1.18, 0.5, 1);

    /// <summary>The ambient glow drifting from one point to the next.</summary>
    public static readonly CubicBezier Glow = new(0.37, 0, 0.63, 1);

    public double Ease(double t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return Y(Solve(t));
    }

    private static double Bezier(double s, double p1, double p2) =>
        3 * (1 - s) * (1 - s) * s * p1 + 3 * (1 - s) * s * s * p2 + s * s * s;

    private double X(double s) => Bezier(s, x1, x2);

    private double Y(double s) => Bezier(s, y1, y2);

    /// <summary>The curve parameter whose x is <paramref name="t"/>: Newton first, bisection when the slope is too flat.</summary>
    private double Solve(double t)
    {
        var s = t;
        for (var i = 0; i < 8; i++)
        {
            var error = X(s) - t;
            if (Math.Abs(error) < 1e-6) return s;
            var slope = 3 * (1 - s) * (1 - s) * x1 + 6 * (1 - s) * s * (x2 - x1) + 3 * s * s * (1 - x2);
            if (Math.Abs(slope) < 1e-6) break;
            s -= error / slope;
        }
        double low = 0, high = 1;
        s = t;
        while (high - low > 1e-7)
        {
            if (X(s) < t) low = s;
            else high = s;
            s = (low + high) / 2;
        }
        return s;
    }
}
