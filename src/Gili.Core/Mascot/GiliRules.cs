using Gili.Core.Motion;

namespace Gili.Core.Mascot;

/// <summary>Where Gili is drawn (PRD 6.3 "Maskot Gili"): inside the idle island, the circle beside it, or beside the tab menu.</summary>
public enum GiliForm { InIsland, Circle, Menu }

/// <summary>
/// Gili's expressions available so far, lowest first in priority. Focus and Surprise come later (PRD 6.3
/// "Ekspresi Gili").
/// </summary>
public enum GiliMood { Neutral, Enjoy, Sleep, Wide, Happy }

/// <summary>What the expression depends on: a mouse drag, time since the last input, music playing in the slot, and a note just home.</summary>
public readonly record struct GiliInputs(bool Dragging, TimeSpan Idle, bool MusicPlayingInSlot, bool Happy = false);

/// <summary>Side, corner radius and gap of Gili's two eyes in one form.</summary>
public readonly record struct GiliEyeSize(double Side, double Radius, double Gap);

/// <summary>Gili's gaze, expressions, blinks and sleep (PRD 6.3 "Maskot Gili", "Ekspresi Gili"; mockup <c>gAim()</c>, <c>gMood()</c>).</summary>
public static class GiliRules
{
    public static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RecentreAfter = TimeSpan.FromSeconds(8);
    public const double DragThreshold = 6;
    public static readonly SpringParameters EyeSpring = new(0.8, 0.25);
    public static readonly SpringParameters ShapeSpring = new(0.8, 0.45);

    private const double IslandReach = 0.4;
    private const double CircleReach = 400;

    /// <summary>The highest expression that applies: happy for a note come home, wide eyes while dragging, then asleep, then enjoying music.</summary>
    public static GiliMood Mood(GiliInputs inputs) =>
        inputs.Happy ? GiliMood.Happy
        : inputs.Dragging ? GiliMood.Wide
        : inputs.Idle >= SleepAfter ? GiliMood.Sleep
        : inputs.MusicPlayingInSlot ? GiliMood.Enjoy
        : GiliMood.Neutral;

    /// <summary>Closed, wide and happy eyes don't blink, on a timer or on a click.</summary>
    public static bool CanBlink(GiliMood mood) => mood is not (GiliMood.Sleep or GiliMood.Wide or GiliMood.Happy);

    /// <summary>How long Gili is happy when a floating note comes home into the closed island.</summary>
    public static readonly TimeSpan HappyFor = TimeSpan.FromMilliseconds(900);

    /// <summary>How long the eyes stay on a note let go outside the island.</summary>
    public static readonly TimeSpan WatchLanded = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Where the eyes look, from −1 to 1 on each axis. Across: the pointer relative to the eyes, at full reach 40% of the
    /// screen away inside the island and 400 px away for the circles. Up and down: the pointer's height on the screen,
    /// full at 40% of the screen height from the middle, so the eyes look up too although the bar sits at the top.
    /// </summary>
    public static (double X, double Y) Aim(
        double pointerX, double pointerY, double eyesCentreX, GiliForm form, double screenWidth, double screenHeight)
    {
        var span = form == GiliForm.InIsland ? screenWidth * IslandReach : CircleReach;
        return (
            Math.Clamp((pointerX - eyesCentreX) / span, -1, 1),
            Math.Clamp((pointerY - screenHeight / 2) / (screenHeight * IslandReach), -1, 1));
    }

    /// <summary>
    /// The eyes' offset from centre for an aim. Inside the island they travel end to end, 9 px from the ends and 3 px
    /// from the top and bottom; the circle leans 2 px and the one beside the menu 3 px.
    /// </summary>
    public static (double X, double Y) Offset(
        double aimX, double aimY, GiliForm form, double islandWidth, double islandHeight, double eyesWidth, double eyesHeight) =>
        form switch
        {
            GiliForm.InIsland => (
                aimX * Math.Max(0, (islandWidth - 18 - eyesWidth) / 2),
                aimY * Math.Max(0, (islandHeight - eyesHeight) / 2 - 3)),
            GiliForm.Circle => (aimX * 2, aimY * 2),
            _ => (aimX * 3, aimY * 3),
        };

    /// <summary>A blink every 3–6 s; one in five is a double blink, which waits 350 ms longer.</summary>
    public static (TimeSpan Wait, bool Double) NextBlink(Random random)
    {
        var twice = random.NextDouble() < 0.2;
        return (TimeSpan.FromMilliseconds(3000 + random.NextDouble() * 3000 + (twice ? 350 : 0)), twice);
    }

    public static GiliEyeSize Eyes(GiliForm form, int islandHeight) => form switch
    {
        GiliForm.InIsland => new(islandHeight * 0.42, 3, 2),
        GiliForm.Circle => new(6, 2, 2),
        _ => new(9, 3, 3),
    };
}
