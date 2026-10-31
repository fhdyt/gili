using Gili.Core.Motion;

namespace Gili.Core.Timers;

/// <summary>
/// The island Timer page's iOS-style picker (F-ISL-29): hours 0–23, minutes and seconds 0–59, so a timer can be set to
/// any length up to 23:59:59.
/// </summary>
public static class TimerWheel
{
    public const int MaxSeconds = 23 * 3600 + 59 * 60 + 59;

    public static (int Hours, int Minutes, int Seconds) Split(int total)
    {
        var t = Math.Clamp(total, 0, MaxSeconds);
        return (t / 3600, t / 60 % 60, t % 60);
    }

    public static int Combine(int hours, int minutes, int seconds) => hours * 3600 + minutes * 60 + seconds;
}

/// <summary>
/// One looping column of the picker. Its position is in rows and never wrapped, so it can spin past 59 back to 0;
/// <see cref="Value"/> is where it settles. A spring carries it between rows, and a new target keeps the motion, so
/// scrolling again mid-way just goes on from what is on screen (PRD 8.3).
/// </summary>
public sealed class WheelColumn
{
    /// <summary>Snapping without a bounce, quick enough to feel like iOS.</summary>
    public static readonly SpringParameters Snap = new(1.0, 0.35);

    /// <summary>How far a fling carries: the release speed (rows per second) times this, like a wheel slowing down.</summary>
    public const double Projection = 0.3;

    /// <summary>The most rows one fling spins.</summary>
    public const int MaxFling = 30;

    private readonly Spring _spring;
    private bool _dragging;

    public WheelColumn(int count, int value)
    {
        Count = count;
        _spring = new Spring(Wrap(value, count)) { Parameters = Snap };
    }

    public int Count { get; }

    /// <summary>The row in the middle right now, in rows, possibly between two.</summary>
    public double Position => _spring.Value;

    /// <summary>The value the column settles on.</summary>
    public int Value => Wrap((int)Math.Round(_spring.Target), Count);

    public bool IsMoving => _dragging || !_spring.IsAtRest;

    public static int Wrap(int value, int count) => ((value % count) + count) % count;

    /// <summary>Mouse wheel notches or arrow keys: whole rows from where it is heading.</summary>
    public void Scroll(int steps) => _spring.Target = Math.Round(_spring.Target) + steps;

    /// <summary>Spins to <paramref name="value"/> the short way round; without animation it jumps there.</summary>
    public void SetValue(int value, bool animate)
    {
        var from = Math.Round(_spring.Target);
        var diff = Wrap(value - Wrap((int)from, Count), Count);
        if (diff > Count / 2) diff -= Count;
        if (animate) _spring.Target = from + diff;
        else _spring.SnapTo(from + diff);
    }

    public void BeginDrag()
    {
        _dragging = true;
        _spring.SnapTo(_spring.Value);
    }

    /// <summary>The column follows the pointer: positive moves to higher values.</summary>
    public void DragBy(double rows) => _spring.SnapTo(_spring.Value + rows);

    /// <summary>Lets go: the wheel carries on by its speed (rows per second), then settles on a whole row.</summary>
    public void EndDrag(double velocity)
    {
        _dragging = false;
        var fling = Math.Clamp(velocity * Projection, -MaxFling, MaxFling);
        _spring.Target = Math.Round(_spring.Value + fling);
    }

    public void Step(double seconds) => _spring.Step(seconds);

    /// <summary>The row whose middle is nearest to <paramref name="offset"/> rows from the middle row (a click).</summary>
    public int RowAt(double offset) => (int)Math.Round(Position + offset);
}
