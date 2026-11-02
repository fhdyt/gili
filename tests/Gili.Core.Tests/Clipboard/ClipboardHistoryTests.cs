using Gili.Core.Clipboard;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Clipboard;

/// <summary>F-LEFT-03, F-ISL-28: the clipboard history (PRD 6.3 "Halaman Clipboard", design 4a).</summary>
public sealed class ClipboardHistoryTests : IDisposable
{
    private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-clip-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private ClipboardHistory New(IClipboardHistoryStore? store = null) => new(store, () => _now);

    [Fact]
    public void Newest_first_and_the_same_text_moves_up_without_a_second_row()
    {
        var history = New();
        history.Add("one", "Chrome");
        _now = _now.AddMinutes(1);
        history.Add("two", "Slack");
        _now = _now.AddMinutes(1);
        history.Add("one", "Word");
        Assert.Equal(["one", "two"], history.Items.Select(e => e.Text));
        Assert.Equal(("Word", _now), (history.Items[0].SourceApp, history.Items[0].CopiedAt));
    }

    [Fact]
    public void Blank_text_is_ignored_and_long_text_is_cut()
    {
        var history = New();
        history.Add("  \r\n ", null);
        Assert.Empty(history.Items);
        history.Add(new string('a', ClipboardHistory.MaxLength + 10), null);
        Assert.Equal(ClipboardHistory.MaxLength, history.Items[0].Text.Length);
    }

    [Fact]
    public void Fifty_at_most_and_the_oldest_goes()
    {
        var history = New();
        for (var i = 0; i < 52; i++)
        {
            _now = _now.AddSeconds(1);
            history.Add("t" + i, null);
        }
        Assert.Equal(50, history.Items.Count);
        Assert.Equal("t51", history.Items[0].Text);
        Assert.DoesNotContain(history.Items, e => e.Text is "t0" or "t1");
    }

    [Fact]
    public void Pasting_moves_a_row_up_and_remove_and_clear_work()
    {
        var history = New();
        history.Add("a", null);
        history.Add("b", null);
        _now = _now.AddHours(1);
        history.Use(history.Items[1].Id);
        Assert.Equal(("a", _now), (history.Items[0].Text, history.Items[0].CopiedAt));
        history.Remove(history.Items[0].Id);
        Assert.Equal(["b"], history.Items.Select(e => e.Text));
        history.Clear();
        Assert.Empty(history.Items);
    }

    [Fact]
    public void Search_ignores_case()
    {
        var history = New();
        history.Add("Jl. Kemang Raya No. 12", null);
        history.Add("INV-2026-1043", null);
        Assert.Equal(["Jl. Kemang Raya No. 12"], history.Search(" kemang ").Select(e => e.Text));
        Assert.Equal(2, history.Search("").Count);
        Assert.Empty(history.Search("zzz"));
    }

    [Theory]
    [InlineData(0.5, "now")]
    [InlineData(38, "38 min")]
    [InlineData(125, "2 h")]
    [InlineData(26 * 60, "Yesterday")]
    [InlineData(80 * 60, "3 days")]
    public void Ago_reads_like_the_mockup(double minutes, string expected) =>
        Assert.Equal(expected, ClipboardText.Ago(_now.AddMinutes(-minutes), _now));

    [Fact]
    public void A_row_is_one_line_with_its_app_and_time()
    {
        Assert.Equal("a b c", ClipboardText.Line("  a\r\n\tb   c "));
        Assert.Equal("Chrome · 38 min", ClipboardText.Detail(new ClipboardEntry(1, "x", "Chrome", _now.AddMinutes(-38)), _now));
        Assert.Equal("now", ClipboardText.Detail(new ClipboardEntry(1, "x", null, _now), _now));
    }

    [Fact]
    public void The_history_survives_a_restart()
    {
        using (var db = Database.Open(_folder))
        {
            var history = New(new ClipboardHistoryStore(db));
            history.Add("first", "Chrome");
            _now = _now.AddMinutes(1);
            history.Add("second", null);
            _now = _now.AddMinutes(1);
            history.Add("first", "Notepad");
            history.Remove(history.Items[1].Id);
        }
        using (var db = Database.Open(_folder))
        {
            var history = New(new ClipboardHistoryStore(db));
            Assert.Equal([("first", "Notepad")], history.Items.Select(e => (e.Text, e.SourceApp)));
        }
    }

    [Fact]
    public void Copies_from_before_this_sign_in_are_gone_with_their_images()
    {
        var signIn = _now.AddMinutes(30);
        using (var db = Database.Open(_folder))
        {
            var history = New(new ClipboardHistoryStore(db));
            history.Add("yesterday", "Chrome");
            history.AddImage("old.png", "Screenshot", 10, 10, null);
            _now = signIn.AddMinutes(1);
            history.Add("today", "Word");
        }
        var discarded = new List<string>();
        using (var db = Database.Open(_folder))
        {
            var history = new ClipboardHistory(new ClipboardHistoryStore(db), () => _now, discarded.Add, since: signIn);
            Assert.Equal(["today"], history.Items.Select(e => e.Text));
            Assert.Equal(["old.png"], discarded);
        }
        using (var db = Database.Open(_folder))
            Assert.Equal(["today"], New(new ClipboardHistoryStore(db)).Items.Select(e => e.Text));
    }
}
