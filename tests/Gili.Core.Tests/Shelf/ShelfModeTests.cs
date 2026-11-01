using Gili.Core.Shelf;

namespace Gili.Core.Tests.Shelf;

public class ShelfModeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private const string Kept = @"C:\Users\me\AppData\Local\Gili\shelf\9f2c";

    private readonly Dictionary<string, FileFacts> _disk = new()
    {
        [@"C:\a.pdf"] = new(false, 2_000),
        [Kept + @"\report.docx"] = new(false, 4_000),
    };

    private readonly List<string> _discarded = [];

    private ShelfService Create() => new(null, p => _disk.TryGetValue(p, out var f) ? f : null, _discarded.Add);

    [Fact]
    public void A_drop_keeps_its_column_and_a_copy_is_the_default()
    {
        var shelf = Create();
        Assert.Equal(ShelfMode.Copy, shelf.Add([@"C:\a.pdf"], Now)!.Mode);
        Assert.Equal(ShelfMode.Cut, shelf.Add([@"C:\a.pdf"], Now, ShelfMode.Cut)!.Mode);
    }

    [Fact]
    public void Removing_an_item_kept_by_gili_deletes_its_folder()
    {
        var shelf = Create();
        var kept = shelf.Add([Kept + @"\report.docx"], Now, ShelfMode.Copy, Kept)!;
        var reference = shelf.Add([@"C:\a.pdf"], Now)!;
        Assert.Equal(Kept, kept.Folder);
        Assert.True(kept.IsKept);
        Assert.False(reference.IsKept);
        shelf.Remove(reference.Id);
        Assert.Empty(_discarded);
        shelf.Remove(kept.Id);
        Assert.Equal([Kept], _discarded);
    }

    [Fact]
    public void Clear_deletes_every_kept_folder()
    {
        var shelf = Create();
        shelf.Add([Kept + @"\report.docx"], Now, ShelfMode.Cut, Kept);
        shelf.Add([@"C:\a.pdf"], Now);
        shelf.Clear();
        Assert.Equal([Kept], _discarded);
    }

    [Fact]
    public void A_kept_item_that_was_moved_out_deletes_its_folder()
    {
        var shelf = Create();
        var kept = shelf.Add([Kept + @"\report.docx"], Now, ShelfMode.Cut, Kept)!;
        _disk.Remove(Kept + @"\report.docx");
        Assert.True(shelf.PruneItem(kept.Id));
        Assert.Empty(shelf.Items);
        Assert.Equal([Kept], _discarded);
    }

    [Fact]
    public void A_copy_taken_out_leaves_the_shelf_and_keeps_its_file()
    {
        var shelf = Create();
        var item = shelf.Add([@"C:\a.pdf"], Now)!;
        Assert.True(shelf.TakeOut(item.Id, Now));
        Assert.Empty(shelf.Items);
        Assert.Empty(_discarded);
    }

    [Fact]
    public void Taking_out_a_missing_item_changes_nothing() => Assert.False(Create().TakeOut(42, Now));

    [Fact]
    public void A_kept_folder_taken_out_is_deleted_only_after_half_an_hour()
    {
        var shelf = Create();
        var kept = shelf.Add([Kept + @"\report.docx"], Now, ShelfMode.Copy, Kept)!;
        shelf.TakeOut(kept.Id, Now);
        Assert.Empty(shelf.Items);
        // Explorer may still be copying out of it, and the next start must not sweep it away either.
        Assert.Equal([Kept], shelf.KeptFolders);
        shelf.SweepLeftovers(Now.AddMinutes(29));
        Assert.Empty(_discarded);
        shelf.SweepLeftovers(Now.AddMinutes(30));
        Assert.Equal([Kept], _discarded);
        Assert.Empty(shelf.KeptFolders);
        shelf.SweepLeftovers(Now.AddMinutes(31));
        Assert.Equal([Kept], _discarded);
    }

    [Fact]
    public void Kept_folders_lists_what_the_items_still_use()
    {
        var shelf = Create();
        shelf.Add([Kept + @"\report.docx"], Now, ShelfMode.Copy, Kept);
        shelf.Add([@"C:\a.pdf"], Now);
        Assert.Equal([Kept], shelf.KeptFolders);
    }

    [Theory]
    [InlineData(@"C:\Users\me\AppData\Local\Temp\7zO1234\photo.jpg", true)]
    [InlineData(@"C:\Users\me\AppData\Local\Temp\Temp1_pack.zip\pack\a.txt", true)]
    [InlineData(@"c:\users\ME\appdata\local\temp\x.txt", true)]
    [InlineData(@"C:\Users\me\Downloads\a.pdf", false)]
    [InlineData(@"C:\Users\me\AppData\Local\TempOther\a.pdf", false)]
    public void Files_in_the_temp_folder_are_copied_because_they_vanish(string path, bool copy) =>
        Assert.Equal(copy, ShelfDrop.MustKeep(path, @"C:\Users\me\AppData\Local\Temp\"));

    [Theory]
    [InlineData("report.docx", "report.docx")]
    [InlineData(@"pack\docs\a.txt", @"pack\docs\a.txt")]
    [InlineData("pack/docs/a.txt", @"pack\docs\a.txt")]
    [InlineData(@"..\..\evil.exe", "evil.exe")]
    [InlineData(@"C:\Windows\evil.exe", @"Windows\evil.exe")]
    [InlineData("a:b?.txt", "a_b_.txt")]
    [InlineData("", "File")]
    public void Names_from_a_virtual_drop_stay_inside_the_folder(string name, string expected) =>
        Assert.Equal(expected, ShelfDrop.SafeRelativePath(name));

    [Fact]
    public void The_top_entries_become_the_items_paths()
    {
        Assert.Equal(["pack", "readme.txt"], ShelfDrop.TopLevel([@"pack\a.txt", @"pack\docs\b.txt", "readme.txt", "pack"]));
    }

    [Fact]
    public void Virtual_drops_have_a_size_limit()
    {
        Assert.True(ShelfDrop.FitsLimit(ShelfDrop.MaxKeptBytes));
        Assert.False(ShelfDrop.FitsLimit(ShelfDrop.MaxKeptBytes + 1));
    }

    [Fact]
    public void Tooltips_say_cut_and_kept()
    {
        var facts = new[] { (@"C:\a.pdf", (FileFacts?)new FileFacts(false, 2_000)) };
        Assert.Equal("a.pdf · 2 KB", ShelfText.Tooltip(facts, ShelfMode.Copy, kept: false));
        Assert.Equal("Cut · moves when you drag it out\na.pdf · 2 KB", ShelfText.Tooltip(facts, ShelfMode.Cut, kept: false));
        Assert.Equal("a.pdf · 2 KB\nKept by Gili until you drag it out", ShelfText.Tooltip(facts, ShelfMode.Copy, kept: true));
    }
}
