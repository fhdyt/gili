using Gili.Core.Island;
using Gili.Core.Media;
using Gili.Core.Power;

namespace Gili.Core.Demo;

/// <summary>One thing the demo does to the island (F-BAR-09).</summary>
public abstract record DemoAction;

/// <summary>A copied text, shown as the clipboard snippet (F-ISL-17).</summary>
public sealed record DemoCopy(string Text) : DemoAction;

/// <summary>A timer that runs for the demo only and is never saved (F-TOOL-01).</summary>
public sealed record DemoTimer(int Seconds) : DemoAction;

/// <summary>An alarm that rings for the demo only and is never saved (F-TOOL-05).</summary>
public sealed record DemoAlarm(TimeOnly Time, string Label) : DemoAction
{
    public const string CardId = "alarm:demo";
}

/// <summary>A task reminder for the demo only, never saved (F-TOOL-07); it closes itself after 10 s.</summary>
public sealed record DemoReminder(string Title, TimeOnly Time) : DemoAction
{
    public const string CardId = "todo:demo";
}

/// <summary>Answers a card the demo raised.</summary>
public sealed record DemoDismiss(string Card) : DemoAction;

/// <summary>Fake media replaces the real session; null means nothing is playing.</summary>
public sealed record DemoMedia(MediaState? State) : DemoAction;

public sealed record DemoPower(PowerSnapshot Power) : DemoAction;

public sealed record DemoOpen(IslandPage Page) : DemoAction;

public sealed record DemoClose : DemoAction;

public sealed record DemoEnd : DemoAction;

public sealed record DemoStep(TimeSpan At, DemoAction Action);

/// <summary>
/// The demo of every Phase 1 island state, about 45 s (plan 1g decision 2): a copy in the slot, a 10 s timer and its card, music in the slot, the charger as a wide
/// notice and a bubble, the Media page, a walk through the tabs, the Power island, the charger out, then the music
/// pausing and leaving so Gili returns to the island.
/// </summary>
public static class DemoScript
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(243);
    private static readonly TimeSpan StartPosition = TimeSpan.FromSeconds(42);

    public static IReadOnlyList<DemoStep> Phase1(DateTimeOffset start)
    {
        // A copy, then a 10 s timer that ends in its card while music plays in the bubble; then the rest (plan 2b).
        var playAt = TimeSpan.FromSeconds(6.7);
        var pauseAt = TimeSpan.FromSeconds(39);
        var song = new MediaState("Midnight City", "M83", "Spotify.exe", MediaKind.Music, IsPlaying: true, StartPosition,
            Duration, start + playAt, CanSeek: true, CanSkip: true, Cover: null);
        var paused = song with { IsPlaying = false, Position = StartPosition + (pauseAt - playAt), PositionUpdatedAt = start + pauseAt };
        return
        [
            Step(0.3, new DemoCopy("Meeting at 3 in the Merapi room, bring a charger")),
            Step(3.5, new DemoTimer(10)),
            Step(6.7, new DemoMedia(song)),
            Step(16.5, new DemoDismiss("timer")),
            Step(18, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: true, Charging: true, Percent: 64))),
            Step(22, new DemoOpen(IslandPage.Media)),
            Step(24.5, new DemoOpen(IslandPage.Timer)),
            Step(26, new DemoOpen(IslandPage.Calendar)),
            Step(27.5, new DemoOpen(IslandPage.Todo)),
            Step(29.5, new DemoClose()),
            Step(31, new DemoOpen(IslandPage.Controls)),
            Step(34, new DemoClose()),
            Step(35.5, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: false, Charging: false, Percent: 64))),
            Step(36.5, new DemoAlarm(new TimeOnly(14, 30), "Take medicine")),
            Step(39, new DemoMedia(paused)),
            Step(40.5, new DemoDismiss(DemoAlarm.CardId)),
            Step(41.5, new DemoMedia(null)),
            Step(42, new DemoReminder("Prepare meeting slides", new TimeOnly(15, 0))),
            Step(49, new DemoEnd()),
        ];
    }

    private static DemoStep Step(double seconds, DemoAction action) => new(TimeSpan.FromSeconds(seconds), action);
}
