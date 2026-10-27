using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public class NoteTextTests
{
    private static readonly TimeZoneInfo Jakarta = TimeZoneInfo.CreateCustomTimeZone("plus7", TimeSpan.FromHours(7), "+07", "+07");
    private static readonly TimeSpan Wib = TimeSpan.FromHours(7);
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 20, 0, 0, Wib); // Tuesday

    [Fact]
    public void Title_is_the_first_line()
    {
        Assert.Equal("Groceries", NoteText.Title("  Groceries \nmilk\neggs"));
        Assert.Equal("milk\neggs", NoteText.Rest("Groceries\n milk\neggs "));
        Assert.Equal("", NoteText.Rest("Only a title"));
        Assert.Equal("Groceries", NoteText.Title("Groceries\r\nmilk"));
    }

    [Fact]
    public void Blank_bodies_are_blank()
    {
        Assert.True(NoteText.IsBlank("  \n \t"));
        Assert.True(NoteText.IsBlank(""));
        Assert.False(NoteText.IsBlank(" a "));
    }

    [Fact]
    public void Untitled_note_label()
    {
        Assert.Equal("Untitled note", NoteText.Label("  \n body"));
        Assert.Equal("Plan", NoteText.Label("Plan\nbody"));
    }

    [Fact]
    public void Search_ignores_case_and_blank_queries()
    {
        Assert.True(NoteText.Matches("Buy MILK", "milk"));
        Assert.True(NoteText.Matches("Title\nsecond line", "SECOND"));
        Assert.False(NoteText.Matches("Buy milk", "eggs"));
        Assert.True(NoteText.Matches("anything", ""));
        Assert.True(NoteText.Matches("anything", "   "));
    }

    [Fact]
    public void Edited_reads_like_the_mockup()
    {
        Assert.Equal("19:20", NoteText.Edited(new DateTimeOffset(2026, 10, 27, 19, 20, 0, Wib), Now, Jakarta, true));
        Assert.Equal("7:20 PM", NoteText.Edited(new DateTimeOffset(2026, 10, 27, 19, 20, 0, Wib), Now, Jakarta, false));
        Assert.Equal("Yesterday", NoteText.Edited(new DateTimeOffset(2026, 10, 26, 9, 0, 0, Wib), Now, Jakarta, true));
        Assert.Equal("Yesterday", NoteText.Edited(new DateTimeOffset(2026, 10, 26, 23, 59, 0, Wib), new DateTimeOffset(2026, 10, 27, 0, 1, 0, Wib), Jakarta, true));
        Assert.Equal("Wed", NoteText.Edited(new DateTimeOffset(2026, 10, 21, 9, 0, 0, Wib), Now, Jakarta, true));
        Assert.Equal("12 Oct", NoteText.Edited(new DateTimeOffset(2026, 10, 12, 9, 0, 0, Wib), Now, Jakarta, true));
        Assert.Equal("12 Oct 2025", NoteText.Edited(new DateTimeOffset(2025, 10, 12, 9, 0, 0, Wib), Now, Jakarta, true));
    }

    [Fact]
    public void Edited_uses_the_zone_not_the_offset()
    {
        // 17:30 UTC on 26 Oct is 00:30 on 27 Oct in WIB.
        Assert.Equal("00:30", NoteText.Edited(new DateTimeOffset(2026, 10, 26, 17, 30, 0, TimeSpan.Zero), Now, Jakarta, true));
    }

    [Fact]
    public void Saved_shows_the_time()
    {
        Assert.Equal("Saved · 19:20", NoteText.Saved(new DateTimeOffset(2026, 10, 27, 19, 20, 0, Wib), Jakarta, true));
        Assert.Equal("Saved · 7:20 PM", NoteText.Saved(new DateTimeOffset(2026, 10, 27, 19, 20, 0, Wib), Jakarta, false));
    }

    [Fact]
    public void Fixed_texts()
    {
        Assert.Equal("Empty note", NoteText.Empty);
        Assert.Equal("No notes match.", NoteText.NoMatch);
        Assert.Equal("No notes yet.", NoteText.NoNotes);
    }
}
