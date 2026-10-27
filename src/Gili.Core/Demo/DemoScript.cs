using Gili.Core.Island;
using Gili.Core.Media;
using Gili.Core.Power;

namespace Gili.Core.Demo;

/// <summary>One thing the demo does to the island (F-BAR-09).</summary>
public abstract record DemoAction;

/// <summary>Fake media replaces the real session; null means nothing is playing.</summary>
public sealed record DemoMedia(MediaState? State) : DemoAction;

public sealed record DemoPower(PowerSnapshot Power) : DemoAction;

public sealed record DemoOpen(IslandPage Page) : DemoAction;

public sealed record DemoClose : DemoAction;

public sealed record DemoEnd : DemoAction;

public sealed record DemoStep(TimeSpan At, DemoAction Action);

/// <summary>
/// The demo of every Phase 1 island state, about 30 s (plan 1g decision 2): music in the slot, the charger as a wide
/// notice and a bubble, the Media page, a walk through the tabs, the Power island, the charger out, then the music
/// pausing and leaving so Gili returns to the island.
/// </summary>
public static class DemoScript
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(243);
    private static readonly TimeSpan StartPosition = TimeSpan.FromSeconds(42);

    public static IReadOnlyList<DemoStep> Phase1(DateTimeOffset start)
    {
        var playAt = TimeSpan.FromSeconds(0.3);
        var pauseAt = TimeSpan.FromSeconds(24.5);
        var song = new MediaState("Midnight City", "M83", "Spotify.exe", MediaKind.Music, IsPlaying: true, StartPosition,
            Duration, start + playAt, CanSeek: true, CanSkip: true, Cover: null);
        var paused = song with { IsPlaying = false, Position = StartPosition + (pauseAt - playAt), PositionUpdatedAt = start + pauseAt };
        return
        [
            Step(0.3, new DemoMedia(song)),
            Step(3.5, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: true, Charging: true, Percent: 64))),
            Step(7.5, new DemoOpen(IslandPage.Media)),
            Step(10, new DemoOpen(IslandPage.Timer)),
            Step(11.5, new DemoOpen(IslandPage.Calendar)),
            Step(13, new DemoOpen(IslandPage.Todo)),
            Step(15, new DemoClose()),
            Step(16.5, new DemoOpen(IslandPage.Controls)),
            Step(19.5, new DemoClose()),
            Step(21, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: false, Charging: false, Percent: 64))),
            Step(24.5, new DemoMedia(paused)),
            Step(27, new DemoMedia(null)),
            Step(29, new DemoEnd()),
        ];
    }

    private static DemoStep Step(double seconds, DemoAction action) => new(TimeSpan.FromSeconds(seconds), action);
}
