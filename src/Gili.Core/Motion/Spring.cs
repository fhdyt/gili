namespace Gili.Core.Motion;

/// <summary>A spring as Apple's motion guidelines describe it (PRD 8.3): damping ratio (1 = no bounce) and response in seconds.</summary>
public readonly record struct SpringParameters(double DampingRatio, double Response)
{
    /// <summary>Compact to expanded, card or drop zone, and resizing while floating: a slight Dynamic Island bounce.</summary>
    public static readonly SpringParameters Open = new(0.82, 0.42);

    /// <summary>Floating back to compact, without bounce.</summary>
    public static readonly SpringParameters Close = new(1.0, 0.32);

    /// <summary>Compact ↔ wide, slot content changes and bar or island height changes.</summary>
    public static readonly SpringParameters Resize = new(0.85, 0.30);

    public static readonly SpringParameters Hover = new(0.8, 0.30);

    /// <summary>Activity bubbles and privacy dots appearing (scale 0 → 1).</summary>
    public static readonly SpringParameters Pop = new(0.7, 0.32);
}

/// <summary>
/// A damped spring stepped once per frame. Changing <see cref="Target"/> keeps the value and velocity, so every
/// transition starts from what is on screen and can be interrupted (PRD 8.3).
/// </summary>
public sealed class Spring(double value)
{
    private const double SubStep = 1.0 / 480;
    private const double MaxFrame = 0.1;
    private const double RestDistance = 0.01;
    private const double RestVelocity = 0.05;

    public double Value { get; private set; } = value;
    public double Velocity { get; private set; }
    public double Target { get; set; } = value;
    public SpringParameters Parameters { get; set; } = SpringParameters.Resize;
    public bool IsAtRest => Value == Target && Velocity == 0;

    public void SnapTo(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    public void Step(double seconds)
    {
        if (IsAtRest) return;
        var stiffness = Math.Pow(2 * Math.PI / Parameters.Response, 2);
        var damping = 4 * Math.PI * Parameters.DampingRatio / Parameters.Response;
        // A stalled frame (debugger, resume) only advances a tenth of a second instead of flinging the value.
        for (var left = Math.Min(seconds, MaxFrame); left > 0; left -= SubStep)
        {
            var dt = Math.Min(left, SubStep);
            Velocity += (-stiffness * (Value - Target) - damping * Velocity) * dt;
            Value += Velocity * dt;
        }
        if (Math.Abs(Value - Target) < RestDistance && Math.Abs(Velocity) < RestVelocity)
        {
            Value = Target;
            Velocity = 0;
        }
    }
}
