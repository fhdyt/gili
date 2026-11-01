using System.Globalization;
using System.Text.RegularExpressions;

namespace Gili.Core.FileOps;

/// <summary>What Explorer is doing (F-ISL-15): copy, move, delete, or one Gili can't read (another Windows language).</summary>
public enum FileOpKind { Copy, Move, Delete, Other }

/// <summary>Why Explorer stopped and waits: a file with the same name, a file in use, or a folder that needs admin.</summary>
public enum FileOpConflict { None, Exists, InUse, Admin }

/// <summary>The slot's state for file operations: running, finished, cancelled, or waiting for a decision.</summary>
public enum FileOpPhase { Running, Done, Cancelled, Attention }

/// <summary>
/// One operation tile as UI Automation reads it from Explorer's progress dialog (<c>OperationStatusWindow</c>, tiles
/// <c>eltTile1…</c>): the tile's name ("Copying 1 item from src to dst - 13% complete"), the item name, what is left
/// ("1 (3,47 GB)"), and, when Explorer waits, its question and which answers it offers.
/// </summary>
public sealed record FileOpTile(string Name, string? ItemName = null, string? Remaining = null, string? Question = null,
    bool CanReplace = false, bool CanSkip = false, bool CanRetry = false, bool NeedsAdmin = false);

/// <summary>One operation as the island shows it.</summary>
public sealed record FileOp(FileOpKind Kind, string Label, int Percent, bool Cancelling, FileOpConflict Conflict, string? Question, string Tooltip);

/// <summary>What the slot shows for Explorer's operations right now.</summary>
public sealed record FileOpSlot(FileOpKind Kind, string Label, int Percent, FileOpPhase Phase, string Tooltip);

public static partial class FileOpRules
{
    public const int LabelLength = 28;

    /// <summary>Reads one tile; a Windows language Gili can't read still gives its percent, as an "Other" operation.</summary>
    public static FileOp Parse(FileOpTile tile)
    {
        var m = TileName().Match(tile.Name);
        var percent = Percent(tile.Name);
        var cancelling = tile.Name.Contains("Cancelling", StringComparison.OrdinalIgnoreCase);
        var conflict = tile.NeedsAdmin ? FileOpConflict.Admin
            : tile.CanRetry ? FileOpConflict.InUse
            : tile.CanReplace || tile.CanSkip ? FileOpConflict.Exists
            : FileOpConflict.None;
        if (!m.Success)
            return new FileOp(FileOpKind.Other, "File operation", percent, cancelling, conflict, tile.Question, tile.Name);
        var verb = m.Groups["verb"].Value;
        var to = m.Groups["to"].Success ? m.Groups["to"].Value : null;
        var kind = verb switch
        {
            "Copying" => FileOpKind.Copy,
            "Moving" when to is not null && to.Equals("Recycle Bin", StringComparison.OrdinalIgnoreCase) => FileOpKind.Delete,
            "Moving" => FileOpKind.Move,
            _ => FileOpKind.Delete,
        };
        var count = int.TryParse(m.Groups["n"].Value.Replace(",", "").Replace(".", "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        var label = count == 1 && !string.IsNullOrWhiteSpace(tile.ItemName) ? tile.ItemName!
            : count == 1 ? "1 item"
            : count.ToString("N0", CultureInfo.InvariantCulture) + " items";
        var size = tile.Remaining is { } left && SizeIn().Match(left) is { Success: true } s ? " · " + s.Groups[1].Value : "";
        var where = m.Groups["from"].Success ? " from " + m.Groups["from"].Value + (to is not null && kind != FileOpKind.Delete ? " to " + to : "") : "";
        return new FileOp(kind, Truncate(label), percent, cancelling, conflict, tile.Question, $"{verb} {label}{where}{size}");
    }

    /// <summary>
    /// Several operations at once: their average percent, with the icon and name of the one furthest from done; the
    /// tooltip lists each (PRD 6.3 "Operasi file Explorer").
    /// </summary>
    public static (FileOpKind Kind, string Label, int Percent, string Tooltip) Combine(IReadOnlyList<FileOp> ops)
    {
        var slowest = ops.MinBy(o => o.Percent)!;
        var percent = (int)Math.Round(ops.Average(o => o.Percent));
        return (slowest.Kind, slowest.Label, percent, string.Join(Environment.NewLine, ops.Select(o => o.Tooltip)));
    }

    /// <summary>The word after a finished operation: Copied, Moved, Deleted.</summary>
    public static string Result(FileOpKind kind, FileOpPhase phase) => phase switch
    {
        FileOpPhase.Cancelled => "Cancelled",
        FileOpPhase.Attention => "Needs attention",
        _ => kind switch
        {
            FileOpKind.Copy => "Copied",
            FileOpKind.Move => "Moved",
            FileOpKind.Delete => "Deleted",
            _ => "Done",
        },
    };

    /// <summary>The conflict card's title (PRD 6.3).</summary>
    public static string ConflictTitle(FileOpConflict conflict) => conflict switch
    {
        FileOpConflict.InUse => "File in use",
        FileOpConflict.Admin => "Needs administrator permission",
        _ => "Replace or skip files",
    };

    public static string Truncate(string text) => text.Length <= LabelLength ? text : text[..(LabelLength - 1)].TrimEnd() + "…";

    private static int Percent(string name) =>
        PercentIn().Matches(name) is { Count: > 0 } all && int.TryParse(all[^1].Groups[1].Value, CultureInfo.InvariantCulture, out var p)
            ? Math.Clamp(p, 0, 100)
            : 0;

    [GeneratedRegex(@"^(?<verb>Copying|Moving|Deleting|Recycling|Discarding)\s+(?<n>[\d.,]+)\s+items?(?:\s+from\s+(?<from>.+?))?(?:\s+to\s+(?<to>.+?))?(?:\s+-\s+[^-]+?)?\s+-\s+\d+%\s+complete$")]
    private static partial Regex TileName();

    [GeneratedRegex(@"(\d+)%")]
    private static partial Regex PercentIn();

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex SizeIn();
}

/// <summary>
/// Follows Explorer's operations from one reading to the next: running while tiles exist, then Copied / Moved / Deleted
/// (or Cancelled) for 2 seconds after the last tile goes (PRD 6.3). A tile that waits for a decision is the conflict.
/// </summary>
public sealed class FileOpTracker
{
    public static readonly TimeSpan ResultFor = TimeSpan.FromSeconds(2);

    private FileOp? _last;
    private bool _cancelling;
    private DateTimeOffset _resultUntil;

    public FileOpSlot? Slot { get; private set; }

    /// <summary>The first operation that waits for a decision; null when none does.</summary>
    public FileOp? Conflict { get; private set; }

    /// <summary>A new reading of the dialog (empty when it is gone); true when the slot or the conflict changed.</summary>
    public bool Update(IReadOnlyList<FileOp> ops, DateTimeOffset now)
    {
        var before = (Slot, Conflict);
        Conflict = ops.FirstOrDefault(o => o.Conflict != FileOpConflict.None);
        if (ops.Count > 0)
        {
            var (kind, label, percent, tooltip) = FileOpRules.Combine(ops);
            _last = ops.MinBy(o => o.Percent);
            _cancelling = ops.Any(o => o.Cancelling);
            Slot = new FileOpSlot(kind, label, percent, Conflict is null ? FileOpPhase.Running : FileOpPhase.Attention, tooltip);
        }
        else if (Slot is { Phase: FileOpPhase.Running or FileOpPhase.Attention } running && _last is not null)
        {
            var phase = _cancelling ? FileOpPhase.Cancelled : FileOpPhase.Done;
            Slot = running with { Percent = phase == FileOpPhase.Done ? 100 : running.Percent, Phase = phase };
            _resultUntil = now + ResultFor;
        }
        else Tick(now);
        return before != (Slot, Conflict);
    }

    /// <summary>The result line leaves after 2 seconds; true when it just did.</summary>
    public bool Tick(DateTimeOffset now)
    {
        if (Slot is not { Phase: FileOpPhase.Done or FileOpPhase.Cancelled } || now < _resultUntil) return false;
        Slot = null;
        _last = null;
        return true;
    }

    /// <summary>When the result line should go, while one shows.</summary>
    public DateTimeOffset? ResultUntil => Slot is { Phase: FileOpPhase.Done or FileOpPhase.Cancelled } ? _resultUntil : null;
}
