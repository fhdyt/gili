using Gili.Core.Island;
using Gili.Core.Media;
using Gili.Core.Power;

namespace Gili.Core.Demo;

/// <summary>One thing the demo does to the island (F-BAR-09).</summary>
public abstract record DemoAction;

/// <summary>A copied text, shown as the clipboard snippet (F-ISL-17).</summary>
public sealed record DemoCopy(string Text) : DemoAction;

/// <summary>Fake media replaces the real session; null means nothing is playing.</summary>
public sealed record DemoMedia(MediaState? State) : DemoAction;

public sealed record DemoPower(PowerSnapshot Power) : DemoAction;

public sealed record DemoOpen(IslandPage Page) : DemoAction;

public sealed record DemoClose : DemoAction;

public sealed record DemoEnd : DemoAction;

public sealed record DemoStep(TimeSpan At, DemoAction Action);

/// <summary>
/// The demo of every Phase 1 island state, about 30 s (plan 1g decision 2): a copy in the slot, music in the slot, the charger as a wide
/// notice and a bubble, the Media page, a walk through the tabs, the Power island, the charger out, then the music
/// pausing and leaving so Gili returns to the island.
/// </summary>
public static class DemoScript
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(243);
    private static readonly TimeSpan StartPosition = TimeSpan.FromSeconds(42);

    public static IReadOnlyList<DemoStep> Phase1(DateTimeOffset start)
    {
        // The copy goes first and keeps the slot 3 s; everything else follows it (plan 1i).
        const double copy = 3.2;
        var playAt = TimeSpan.FromSeconds(0.3 + copy);
        var pauseAt = TimeSpan.FromSeconds(24.5 + copy);
        var song = new MediaState("Midnight City", "M83", "Spotify.exe", MediaKind.Music, IsPlaying: true, StartPosition,
            Duration, start + playAt, CanSeek: true, CanSkip: true, Cover: null);
        var paused = song with { IsPlaying = false, Position = StartPosition + (pauseAt - playAt), PositionUpdatedAt = start + pauseAt };
        return
        [
            Step(0.3, new DemoCopy("Meeting at 3 in the Merapi room, bring a charger")),
            Step(0.3 + copy, new DemoMedia(song)),
            Step(3.5 + copy, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: true, Charging: true, Percent: 64))),
            Step(7.5 + copy, new DemoOpen(IslandPage.Media)),
            Step(10 + copy, new DemoOpen(IslandPage.Timer)),
            Step(11.5 + copy, new DemoOpen(IslandPage.Calendar)),
            Step(13 + copy, new DemoOpen(IslandPage.Todo)),
            Step(15 + copy, new DemoClose()),
            Step(16.5 + copy, new DemoOpen(IslandPage.Controls)),
            Step(19.5 + copy, new DemoClose()),
            Step(21 + copy, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: false, Charging: false, Percent: 64))),
            Step(24.5 + copy, new DemoMedia(paused)),
            Step(27 + copy, new DemoMedia(null)),
            Step(29 + copy, new DemoEnd()),
        ];
    }

    private static DemoStep Step(double seconds, DemoAction action) => new(TimeSpan.FromSeconds(seconds), action);
}
