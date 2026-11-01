using Gili.Core.Devices;
using Gili.Core.Island;
using Gili.Core.Media;
using Gili.Core.Power;

namespace Gili.Core.Demo;

/// <summary>One thing the demo does to the island (F-BAR-09).</summary>
public abstract record DemoAction;

/// <summary>A copied text, shown as the clipboard snippet (F-ISL-17).</summary>
public sealed record DemoCopy(string Text) : DemoAction;

/// <summary>A timer that runs for the demo only and is never saved (F-TOOL-01).</summary>
public sealed record DemoTimer(int Seconds, string? Label = null) : DemoAction;

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

/// <summary>Caps Lock or Num Lock pressed (F-ISL-10).</summary>
public sealed record DemoLockKeys(LockKeys Keys) : DemoAction;

/// <summary>A Bluetooth device connecting or disconnecting (F-ISL-18).</summary>
public sealed record DemoBluetooth(BluetoothChange Change) : DemoAction;

/// <summary>Apps using the camera and the microphone (F-ISL-14).</summary>
public sealed record DemoPrivacy(PrivacyUse Use) : DemoAction;

public sealed record DemoOpen(IslandPage Page) : DemoAction;

/// <summary>Opens a control island, or swaps the open one's control (plan 3b).</summary>
public sealed record DemoControl(ControlKind Kind) : DemoAction;

public sealed record DemoClose : DemoAction;

public sealed record DemoEnd : DemoAction;

/// <summary>Files dragged over the island and dropped: a demo item shows on the Shelf page, never saved (plan 3d).</summary>
public sealed record DemoShelf(string Name, long Size) : DemoAction;

/// <summary>The demo's Shelf item leaves, as if it was dragged out, so the slot it held goes back to the other activities.</summary>
public sealed record DemoShelfGone : DemoAction;

/// <summary>A floating note comes home into the closed island: it bulges once and Gili is happy (plan 2h).</summary>
public sealed record DemoNoteHome : DemoAction;

/// <summary>A demo Claude Code session appears: Claude Desktop with its title, or a terminal named after its folder (plan 3i).</summary>
public sealed record DemoClaudeSession(string Id, string? Title, string Cwd, bool Desktop) : DemoAction;

/// <summary>One of Claude Code's hooks for a demo session (PRD 9.2): a prompt, a question, a tool done, a stop.</summary>
public sealed record DemoClaudeHook(string Event, string Id, string? Tool = null, string? Question = null) : DemoAction;

/// <summary>The demo's 5-hour window, ending <paramref name="SecondsLeft"/> from now; never stored (plan 3j).</summary>
public sealed record DemoClaudeWindow(double SecondsLeft, bool LimitHit) : DemoAction;

public sealed record DemoStep(TimeSpan At, DemoAction Action);

/// <summary>
/// The demo of every island state so far, just under two minutes (plan 1g decision 2, plans 3i and 3j): a copy in the slot, a 10 s timer and its card, music in the slot, the charger as a wide
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
        // A video with its thumbnail in the slot, behind the battery notices (revision 14, issue #31).
        var video = new MediaState("How the James Webb telescope sees", "", "Microsoft.ZuneVideo_8wekyb3d8bbwe!Microsoft.ZuneVideo",
            MediaKind.Video, IsPlaying: true, TimeSpan.FromSeconds(212), TimeSpan.FromSeconds(1012), start + TimeSpan.FromSeconds(52),
            CanSeek: true, CanSkip: false, Cover: null);
        return
        [
            Step(0.3, new DemoCopy("Meeting at 3 in the Merapi room, bring a charger")),
            Step(3.5, new DemoTimer(10, "Tea")),
            // Wide notices fit before the timer's card at 13.5 s (plan 3a): a lock key, then earbuds in and out.
            Step(4, new DemoLockKeys(new LockKeys(Caps: true, Num: false))),
            Step(6, new DemoBluetooth(Buds with { Connected = true })),
            Step(6.7, new DemoMedia(song)),
            Step(10.5, new DemoBluetooth(Buds)),
            Step(16.5, new DemoDismiss("timer")),
            Step(18, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: true, Charging: true, Percent: 64))),
            Step(22, new DemoOpen(IslandPage.Media)),
            // The privacy dots stay beside the open island and the control island.
            Step(22.5, new DemoPrivacy(new PrivacyUse(["Zoom"], ["Discord"]))),
            Step(24.5, new DemoOpen(IslandPage.Timer)),
            Step(26, new DemoOpen(IslandPage.Calendar)),
            Step(27.5, new DemoOpen(IslandPage.Todo)),
            // The System page shows the laptop's own readings (plan 3c).
            Step(28.5, new DemoOpen(IslandPage.System)),
            // A file dropped on the Shelf: 1 s of drop zone, then the page with a demo item that is never saved (plan 3d).
            Step(29, new DemoShelf("Quarterly report.pdf", 2_400_000)),
            // A control island, then another bar icon swaps its control without closing (plan 3b).
            Step(31, new DemoControl(ControlKind.Power)),
            Step(32.5, new DemoControl(ControlKind.Volume)),
            Step(33, new DemoPrivacy(PrivacyUse.None)),
            Step(34, new DemoClose()),
            Step(34.6, new DemoNoteHome()),
            Step(35.5, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: false, Charging: false, Percent: 64))),
            Step(36.5, new DemoAlarm(new TimeOnly(14, 30), "Take medicine")),
            Step(39, new DemoMedia(paused)),
            Step(40.5, new DemoDismiss(DemoAlarm.CardId)),
            Step(41.5, new DemoMedia(null)),
            // The dropped file held the closed island's slot since 34 s (issue 37); it leaves before the reminder.
            Step(41.8, new DemoShelfGone()),
            Step(42, new DemoReminder("Prepare meeting slides", new TimeOnly(15, 0))),
            // After the reminder: the battery runs low, then a charge limit holds the charge (plan 3a).
            Step(52, new DemoMedia(video)),
            Step(52.2, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: false, Charging: false, Percent: 20))),
            Step(57.4, new DemoPower(new PowerSnapshot(HasBattery: true, PluggedIn: true, Charging: false, Percent: 79))),
            Step(59.5, new DemoMedia(null)),
            // Claude (plan 3i): Desktop works, asks (card and Ping, 8 s), is answered; a terminal session joins; both finish
            // behind other windows, so a green dot, then the green bubble with 2, then the Claude page.
            Step(60, new DemoClaudeSession(Desk, "Integrate taskbar with Claude", @"C:\Projects\gili", Desktop: true)),
            Step(60, new DemoClaudeSession(Term, null, @"C:\Projects\top-taskbar", Desktop: false)),
            Step(60.2, new DemoClaudeHook("UserPromptSubmit", Desk)),
            Step(63, new DemoClaudeHook("PreToolUse", Desk, "AskUserQuestion", "Which island layout do you prefer for the session list?")),
            Step(72.5, new DemoClaudeHook("PostToolUse", Desk)),
            Step(73.5, new DemoClaudeHook("UserPromptSubmit", Term)),
            Step(76.5, new DemoClaudeHook("Stop", Desk)),
            Step(81.5, new DemoClaudeHook("Stop", Term)),
            Step(87, new DemoOpen(IslandPage.Claude)),
            Step(91, new DemoClose()),
            // The 5-hour window (plan 3j): the limit reached, then a window that ends 6 s later with its card.
            Step(92, new DemoClaudeWindow(3852, LimitHit: true)),
            Step(97, new DemoClaudeWindow(6, LimitHit: false)),
            Step(108, new DemoDismiss(ResetCard)),
            Step(109.5, new DemoEnd()),
        ];
    }

    /// <summary>The Claude usage reset card's id, as the island raises it.</summary>
    public const string ResetCard = "claude-reset";

    private const string Desk = "demo-desktop";
    private const string Term = "demo-terminal";

    private static readonly BluetoothChange Buds = new("demo", "Galaxy Buds2 Pro", BluetoothKind.Audio, Connected: false, Battery: 80);

    private static DemoStep Step(double seconds, DemoAction action) => new(TimeSpan.FromSeconds(seconds), action);
}
