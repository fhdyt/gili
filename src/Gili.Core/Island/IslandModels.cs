namespace Gili.Core.Island;

public enum IslandMode { Hidden, Compact, Wide, Expanded, Card, DropZone }

/// <summary>Pages of the expanded island, in menu order (PRD 6.3).</summary>
public enum IslandPage { Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, Claude, System }

/// <summary>Activities that can hold the compact slot, best first (PRD 6.3 activity order).</summary>
public enum ActivityKind { ClipboardSnippet, ClaudeAttention, FileOperation, Timer, Stopwatch, ClaudeWorking, Media, Charging }

public enum WideIcon { None, Charging, OnBattery }

/// <summary>A short notice in the wide island, shown as "Text · Value". A newer event with the same key replaces it.</summary>
public sealed record WideEvent(string Key, WideIcon Icon, string Text, string? Value, TimeSpan Duration);

public enum CardPriority { Urgent = 1, Reminder = 2 }

/// <summary>A notification card. Urgent cards stay until answered; reminders close after <paramref name="Timeout"/>.</summary>
public sealed record IslandCard(string Id, CardPriority Priority, TimeSpan Timeout);

/// <summary>What the island shows right now. <see cref="Bubble"/> is only set while compact.</summary>
public sealed record IslandView(
    IslandMode Mode,
    ActivityKind? Slot,
    ActivityKind? Bubble,
    WideEvent? Wide,
    IslandCard? Card,
    IslandPage Page)
{
    public bool IsFloating => Mode is IslandMode.Expanded or IslandMode.Card or IslandMode.DropZone;
}
