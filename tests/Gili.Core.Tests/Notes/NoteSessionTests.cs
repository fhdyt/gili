using Gili.Core.Notes;

namespace Gili.Core.Tests.Notes;

public class NoteSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Typing_waits_half_a_second_after_the_last_key()
    {
        var session = new NoteSession();
        Assert.Null(session.DueAt);
        session.Edit(1, "H", T0);
        Assert.Equal(T0.AddMilliseconds(500), session.DueAt);
        session.Edit(1, "Hi", T0.AddMilliseconds(300));
        Assert.Equal(T0.AddMilliseconds(800), session.DueAt);
    }

    [Fact]
    public void Closing_flushes_a_pending_save()
    {
        var session = new NoteSession();
        session.Edit(1, "Draft", T0);
        session.Edit(2, "Other", T0.AddMilliseconds(100));
        session.Edit(1, "Draft two", T0.AddMilliseconds(200));

        Assert.Equal([(1L, "Draft two"), (2L, "Other")], session.Take());
        Assert.Null(session.DueAt);
        Assert.Empty(session.Take());
    }

    [Fact]
    public void A_note_emptied_and_closed_is_discarded()
    {
        Assert.True(NoteSession.Discards(" \n"));
        Assert.True(NoteSession.Discards(""));
        Assert.False(NoteSession.Discards("a"));
    }
}
