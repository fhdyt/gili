namespace Gili.Core.Island;

public enum IslandMode { Hidden, Compact, Wide, Expanded, Card, DropZone }

/// <summary>
/// Pages of the expanded island, in menu order (PRD 6.3). Controls has no tab: the Power icon (and from Phase 3 Wi-Fi,
/// Bluetooth, volume and brightness) opens it alone, without the menu (PRD 6.4 "Pulau kontrol").
/// </summary>
public enum IslandPage { Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, Claude, System, Controls }

/// <summary>The control a control island shows (PRD 6.4 "Pulau kontrol"): one per bar icon.</summary>
public enum ControlKind { Power, Volume, Brightness, Wifi, Bluetooth }

/// <summary>
/// Activities that can hold the compact slot, best first (PRD 6.3 activity order). Claude's 5-hour window is last while it
/// runs and comes right after a waiting session once its limit is reached. Files on the Shelf come right after the
/// short-lived ones and above every running activity (issue 37).
/// </summary>
public enum ActivityKind { ClipboardSnippet, ClaudeAttention, ClaudeLimit, FileOperation, Shelf, Timer, Stopwatch, ClaudeWorking, Media, Charging, ClaudeWindow }

public enum WideIcon { None, Bolt, Caps, NumLock, Bluetooth, Headphones, Mouse, Keyboard, Gamepad, Phone, Clipboard, TrayDown, Restart }

/// <summary>The colour of a wide notice's icon (PRD 8.3 tokens).</summary>
public enum WideTone { Text, Accent, Warn, Danger, Blue, Muted }

/// <summary>Strong is semibold with tabular digits, Number only tabular digits, Muted the secondary grey.</summary>
public enum WideRunStyle { Normal, Strong, Muted, Number }

/// <summary>One piece of a wide notice's text; <paramref name="SpaceBefore"/> puts the 8 px gap of the mockup before it.</summary>
public sealed record WideRun(string Text, WideRunStyle Style = WideRunStyle.Normal, bool SpaceBefore = false);

/// <summary>A short notice in the wide island. A newer event with the same key replaces it.</summary>
public sealed record WideEvent(string Key, WideIcon Icon, WideTone Tone, IReadOnlyList<WideRun> Runs, TimeSpan Duration)
{
    /// <summary>The notice as one line, a space for each gap: for tests and the accessible name.</summary>
    public string Text => string.Concat(Runs.Select(r => r.SpaceBefore ? " " + r.Text : r.Text));
}

public enum CardPriority { Urgent = 1, Reminder = 2 }

/// <summary>
/// A notification card. Urgent cards stay until answered; reminders close after <paramref name="Timeout"/>. An urgent card
/// that <paramref name="HoldInFocus"/> waits for a pomodoro break like a reminder (Claude usage reset, PRD 6.3).
/// </summary>
public sealed record IslandCard(string Id, CardPriority Priority, TimeSpan Timeout, bool HoldInFocus = false);

/// <summary>
/// What the island shows right now. <see cref="Bubble"/> is only set while compact; <see cref="Control"/> picks the
/// control while <see cref="Page"/> is <see cref="IslandPage.Controls"/>. <see cref="DoneBubble"/> is Claude's green bubble
/// for a session that finished unseen, right of every other bubble.
/// </summary>
public sealed record IslandView(
    IslandMode Mode,
    ActivityKind? Slot,
    ActivityKind? Bubble,
    WideEvent? Wide,
    IslandCard? Card,
    IslandPage Page,
    ControlKind Control = ControlKind.Power,
    bool DoneBubble = false)
{
    public bool IsFloating => Mode is IslandMode.Expanded or IslandMode.Card or IslandMode.DropZone;
}
