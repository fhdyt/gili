using Gili.Core.FileOps;

namespace Gili.Core.Tests.FileOps;

public class FileOpsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    // Names read from Explorer's dialog on the developer's laptop (Windows 11, English, 2026-11-01).
    private static readonly FileOpTile Copy = new("Copying 1 item from src to dst - 13% complete", "Project-Final.bin", "1 (3,47 GB)");

    [Fact]
    public void A_single_copy_shows_its_file_name_percent_and_size()
    {
        var op = FileOpRules.Parse(Copy);
        Assert.Equal(FileOpKind.Copy, op.Kind);
        Assert.Equal("Project-Final.bin", op.Label);
        Assert.Equal(13, op.Percent);
        Assert.Equal(FileOpConflict.None, op.Conflict);
        Assert.Equal("Copying Project-Final.bin from src to dst · 3,47 GB", op.Tooltip);
    }

    [Theory]
    [InlineData("Moving 3 items from Downloads to Photos 2025 - 40% complete", FileOpKind.Move, "3 items", 40)]
    [InlineData("Deleting 1,204 items from Downloads - 12% complete", FileOpKind.Delete, "1,204 items", 12)]
    [InlineData("Moving 1204 items from Downloads to Recycle Bin - 7% complete", FileOpKind.Delete, "1,204 items", 7)]
    [InlineData("Copying 2 items from A to B - Paused - 55% complete", FileOpKind.Copy, "2 items", 55)]
    public void Kinds_counts_and_percent_come_from_the_tile_name(string name, FileOpKind kind, string label, int percent)
    {
        var op = FileOpRules.Parse(new FileOpTile(name));
        Assert.Equal((kind, label, percent), (op.Kind, op.Label, op.Percent));
    }

    [Fact]
    public void Another_language_still_gives_a_percent()
    {
        var op = FileOpRules.Parse(new FileOpTile("Menyalin 1 item dari src ke dst - 64% selesai"));
        Assert.Equal((FileOpKind.Other, 64), (op.Kind, op.Percent));
    }

    [Fact]
    public void Long_names_are_cut_to_28_characters()
    {
        var op = FileOpRules.Parse(Copy with { ItemName = "A very long file name that goes on and on.zip" });
        Assert.True(op.Label.Length <= FileOpRules.LabelLength);
        Assert.EndsWith("…", op.Label);
    }

    [Fact]
    public void A_waiting_tile_is_a_conflict_of_its_kind()
    {
        var exists = new FileOpTile("Copying 1 item from src to dst - Interrupted - 0% complete", "Project-Final.bin", null,
            "The destination already has a file named \"Project-Final.bin\"", CanReplace: true, CanSkip: true);
        Assert.Equal(FileOpConflict.Exists, FileOpRules.Parse(exists).Conflict);
        Assert.Equal(FileOpConflict.InUse, FileOpRules.Parse(exists with { CanReplace = false, CanRetry = true }).Conflict);
        Assert.Equal(FileOpConflict.Admin, FileOpRules.Parse(exists with { NeedsAdmin = true }).Conflict);
        Assert.Equal("Replace or skip files", FileOpRules.ConflictTitle(FileOpConflict.Exists));
    }

    [Fact]
    public void Several_operations_average_and_show_the_slowest()
    {
        var ops = new[]
        {
            FileOpRules.Parse(Copy with { Name = "Copying 1 item from src to dst - 80% complete" }),
            FileOpRules.Parse(new FileOpTile("Deleting 20 items from Old - 20% complete")),
        };
        var (kind, label, percent, tooltip) = FileOpRules.Combine(ops);
        Assert.Equal((FileOpKind.Delete, "20 items", 50), (kind, label, percent));
        Assert.Equal(2, tooltip.Split(Environment.NewLine).Length);
    }

    [Fact]
    public void A_finished_operation_says_copied_for_two_seconds()
    {
        var tracker = new FileOpTracker();
        Assert.True(tracker.Update([FileOpRules.Parse(Copy)], Now));
        Assert.Equal(FileOpPhase.Running, tracker.Slot!.Phase);
        Assert.True(tracker.Update([], Now.AddSeconds(5)));
        Assert.Equal((FileOpPhase.Done, 100), (tracker.Slot!.Phase, tracker.Slot.Percent));
        Assert.Equal("Copied", FileOpRules.Result(tracker.Slot.Kind, tracker.Slot.Phase));
        Assert.False(tracker.Tick(Now.AddSeconds(6.9)));
        Assert.True(tracker.Tick(Now.AddSeconds(7)));
        Assert.Null(tracker.Slot);
    }

    [Fact]
    public void A_cancelled_operation_says_so()
    {
        var tracker = new FileOpTracker();
        tracker.Update([FileOpRules.Parse(Copy with { Name = "Copying 1 item from src to dst - Cancelling - 31% complete" })], Now);
        tracker.Update([], Now.AddSeconds(1));
        Assert.Equal(FileOpPhase.Cancelled, tracker.Slot!.Phase);
        Assert.Equal("Cancelled", FileOpRules.Result(tracker.Slot.Kind, tracker.Slot.Phase));
    }

    [Fact]
    public void A_conflict_holds_the_slot_as_needs_attention_until_answered()
    {
        var tracker = new FileOpTracker();
        var waiting = FileOpRules.Parse(Copy with { CanReplace = true, CanSkip = true, Question = "The destination already has a file" });
        tracker.Update([waiting], Now);
        Assert.Equal(FileOpPhase.Attention, tracker.Slot!.Phase);
        Assert.NotNull(tracker.Conflict);
        tracker.Update([FileOpRules.Parse(Copy)], Now.AddSeconds(1));
        Assert.Null(tracker.Conflict);
        Assert.Equal(FileOpPhase.Running, tracker.Slot!.Phase);
    }

    [Fact]
    public void Nothing_shows_when_no_operation_ran() => Assert.False(new FileOpTracker().Update([], Now));
}
