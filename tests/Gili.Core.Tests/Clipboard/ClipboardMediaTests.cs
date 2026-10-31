using Gili.Core.Clipboard;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Clipboard;

/// <summary>F-ISL-32: images and files in the clipboard history (PRD 6.3 "Halaman Clipboard", revision 12).</summary>
public sealed class ClipboardMediaTests : IDisposable
{
    private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly List<string> _discarded = [];
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-clipmedia-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private ClipboardHistory New(IClipboardHistoryStore? store = null) => new(store, () => _now, _discarded.Add);

    [Fact]
    public void Images_and_files_join_the_history_next_to_text()
    {
        var history = New();
        history.Add("hello", "Notepad");
        history.AddImage("a1.png", "Screenshot", 1920, 1080, "Snipping Tool");
        history.AddFiles([@"C:\docs\report.pdf", @"C:\docs\b.png"], "File Explorer");
        Assert.Equal([ClipKind.Files, ClipKind.Image, ClipKind.Text], history.Items.Select(e => e.Kind));
        var image = history.Items[1];
        Assert.Equal(("a1.png", "Screenshot", 1920, 1080), (image.Text, image.Name, image.Width, image.Height));
        Assert.Equal([@"C:\docs\report.pdf", @"C:\docs\b.png"], history.Items[0].Files);
    }

    [Fact]
    public void The_same_image_or_file_list_moves_up_instead_of_a_second_row()
    {
        var history = New();
        history.AddImage("a1.png", "Screenshot", 10, 10, null);
        history.AddFiles([@"C:\a.txt"], null);
        _now = _now.AddMinutes(1);
        history.AddImage("a1.png", "Screenshot", 10, 10, null);
        Assert.Equal(2, history.Items.Count);
        Assert.Equal(ClipKind.Image, history.Items[0].Kind);
        history.AddFiles([@"C:\a.txt"], null);
        Assert.Equal(2, history.Items.Count);
        Assert.Equal(ClipKind.Files, history.Items[0].Kind);
        Assert.Empty(_discarded);
    }

    [Fact]
    public void At_most_twenty_images_and_the_oldest_png_is_deleted()
    {
        var history = New();
        for (var i = 0; i < ClipboardHistory.MaxImages + 1; i++)
        {
            _now = _now.AddSeconds(1);
            history.AddImage($"{i}.png", "Image", 1, 1, null);
        }
        Assert.Equal(ClipboardHistory.MaxImages, history.Items.Count(e => e.Kind == ClipKind.Image));
        Assert.Equal(["0.png"], _discarded);
        Assert.DoesNotContain(history.Items, e => e.Text == "0.png");
    }

    [Fact]
    public void Removing_and_clearing_delete_the_pngs()
    {
        var history = New();
        history.AddImage("a.png", "Image", 1, 1, null);
        history.AddImage("b.png", "Image", 1, 1, null);
        history.Add("text", null);
        history.Remove(history.Items.Single(e => e.Text == "a.png").Id);
        Assert.Equal(["a.png"], _discarded);
        history.Clear();
        Assert.Equal(["a.png", "b.png"], _discarded);
    }

    [Fact]
    public void An_image_falling_off_the_end_deletes_its_png()
    {
        var history = New();
        history.AddImage("old.png", "Image", 1, 1, null);
        for (var i = 0; i < ClipboardHistory.Max; i++) history.Add("t" + i, null);
        Assert.Equal(["old.png"], _discarded);
    }

    [Fact]
    public void Search_matches_file_names_and_image_names()
    {
        var history = New();
        history.AddFiles([@"C:\docs\Quarterly report.pdf"], null);
        history.AddImage("x.png", "cat.jpg", 1, 1, null);
        history.Add("docs folder", null);
        Assert.Equal([ClipKind.Files], history.Search("quarterly").Select(e => e.Kind));
        Assert.Equal([ClipKind.Image], history.Search("cat").Select(e => e.Kind));
        Assert.Equal([ClipKind.Text], history.Search("docs").Select(e => e.Kind));
    }

    [Fact]
    public void File_entries_whose_files_are_all_gone_are_dropped()
    {
        var history = New();
        history.AddFiles([@"C:\gone.txt"], null);
        history.AddFiles([@"C:\gone2.txt", @"C:\here.txt"], null);
        history.Add("text", null);
        Assert.True(history.PruneFiles(p => p == @"C:\here.txt"));
        Assert.Equal([ClipKind.Text, ClipKind.Files], history.Items.Select(e => e.Kind));
        Assert.False(history.PruneFiles(p => p == @"C:\here.txt"));
    }

    [Fact]
    public void Image_files_lists_the_pngs_still_in_use()
    {
        var history = New();
        history.AddImage("a.png", "Image", 1, 1, null);
        history.Add("t", null);
        Assert.Equal(["a.png"], history.ImageFiles);
    }

    [Fact]
    public void Rows_read_by_kind()
    {
        var now = _now;
        var image = new ClipboardEntry(1, "a.png", "Snipping Tool", now, ClipKind.Image, "Screenshot", 1920, 1080);
        var one = new ClipboardEntry(2, @"C:\docs\report.pdf", null, now, ClipKind.Files);
        var many = new ClipboardEntry(3, "C:\\a.txt\nC:\\b.txt\nC:\\c.txt", null, now, ClipKind.Files);
        Assert.Equal("Screenshot · 1920 × 1080", ClipboardText.Line(image));
        Assert.Equal("report.pdf", ClipboardText.Line(one));
        Assert.Equal("3 files", ClipboardText.Line(many));
        Assert.Equal("Screenshot · 1920 × 1080", ClipboardText.Tooltip(image));
        Assert.Equal("C:\\a.txt\nC:\\b.txt\nC:\\c.txt", ClipboardText.Tooltip(many));
        Assert.Equal("hi there", ClipboardText.Line(new ClipboardEntry(4, "hi\n  there", null, now)));
    }

    [Fact]
    public void The_store_keeps_kind_name_and_size()
    {
        using (var db = Database.Open(_folder))
        {
            var history = new ClipboardHistory(new ClipboardHistoryStore(db), () => _now);
            history.AddImage("a.png", "Screenshot", 640, 480, "Snipping Tool");
            history.AddFiles([@"C:\a.txt", @"C:\b.txt"], "File Explorer");
            history.Add("text", null);
        }
        using (var db = Database.Open(_folder))
        {
            var items = new ClipboardHistoryStore(db).All();
            Assert.Equal([ClipKind.Text, ClipKind.Files, ClipKind.Image], items.Select(e => e.Kind));
            Assert.Equal(("Screenshot", 640, 480), (items[2].Name, items[2].Width, items[2].Height));
            Assert.Equal([@"C:\a.txt", @"C:\b.txt"], items[1].Files);
        }
    }
}
