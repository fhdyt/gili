using System.Windows.Threading;
using Gili.Core.Notes;
using Gili.Data;

namespace Gili.App.Island;

/// <summary>
/// The notes (F-TOOL-08/09): kept in gili.db in the user's order, new ones on top. Typing changes the note in memory at
/// once and writes it 0.5 s after the last key, with one timer per pause and no polling; closing the editor, the island
/// or the app writes what is still waiting. Without the database everything stays in memory. Raised on the UI thread.
/// </summary>
public sealed class NoteService
{
    private readonly NoteStore? _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly NoteSession _session = new();
    private readonly DispatcherTimer _pause = new();
    private readonly List<Note> _notes;
    private DateTimeOffset? _savedAt;

    public NoteService(NoteStore? store, Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _notes = [.. store?.All() ?? []];
        _pause.Tick += (_, _) => Flush();
    }

    public IReadOnlyList<Note> Notes => _notes;

    /// <summary>A note came, went, moved, or took a new colour or size.</summary>
    public event Action? Changed;

    /// <summary>A note's text changed while typing: only its card needs to show it.</summary>
    public event Action<Note>? Edited;

    /// <summary>Whether a save is waiting, and when the last one was written (null before any).</summary>
    public event Action<bool, DateTimeOffset?>? SaveState;

    /// <summary>A note left the island (F-TOOL-12): true when it was Float on top, false when it was dragged out.</summary>
    public event Action<Note, bool>? Floated;

    public int FloatingCount => _notes.Count(n => n.Float is not null);

    /// <summary>
    /// The note leaves the island into a window at this place (DIPs), written at once; its card leaves the grid. False
    /// when six already float or the note floats already. A blank note may float; it is dropped once it comes back.
    /// </summary>
    public bool Float(long id, NoteRect rect, bool fromButton)
    {
        Flush();
        var index = IndexOf(id);
        if (index < 0 || _notes[index].Float is not null || !NoteFloat.CanFloat(FloatingCount)) return false;
        _notes[index] = _notes[index] with { Float = rect };
        _store?.SetFloat(id, rect);
        Changed?.Invoke();
        Floated?.Invoke(_notes[index], fromButton);
        return true;
    }

    /// <summary>A floating note was moved or resized; written once the drag ends, so nothing else needs to know.</summary>
    public void MoveFloat(long id, NoteRect rect)
    {
        var index = IndexOf(id);
        if (index < 0 || _notes[index].Float is not { } was || was == rect) return;
        _notes[index] = _notes[index] with { Float = rect };
        _store?.SetFloat(id, rect);
    }

    /// <summary>Back to island: the note takes its old place in the order again, or is dropped when it is empty (true).</summary>
    public bool Return(long id)
    {
        Flush();
        var index = IndexOf(id);
        if (index < 0 || _notes[index].Float is null) return false;
        _notes[index] = _notes[index] with { Float = null };
        _store?.SetFloat(id, null);
        if (NoteSession.Discards(_notes[index].Body))
        {
            Delete(id);
            return true;
        }
        Changed?.Invoke();
        return false;
    }

    /// <summary>An empty Medium note in the default colour above all others, written at once so its id holds (plan 2f decision 4).</summary>
    public Note Create()
    {
        var now = _clock();
        var note = new Note(0, "", 0, NoteStyle.Default, 0, now, now);
        note = _store?.Add(note) ?? note with
        {
            Id = _notes.Count == 0 ? 1 : _notes.Max(n => n.Id) + 1,
            SortOrder = _notes.Count == 0 ? 0 : _notes.Min(n => n.SortOrder) - 1,
        };
        _notes.Insert(0, note);
        Changed?.Invoke();
        return note;
    }

    /// <summary>The text changes in memory now, line breaks as \n, and is written once typing pauses for 0.5 s.</summary>
    public void Edit(long id, string body)
    {
        body = NoteText.Stored(body);
        var index = IndexOf(id);
        if (index < 0 || _notes[index].Body == body) return;
        var now = _clock();
        _notes[index] = _notes[index] with { Body = body, UpdatedAt = now };
        _session.Edit(id, body, now);
        _pause.Stop();
        var wait = _session.DueAt!.Value - now;
        _pause.Interval = wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(1);
        _pause.Start();
        SaveState?.Invoke(true, _savedAt);
        Edited?.Invoke(_notes[index]);
    }

    public void SetColor(long id, int color) => Restyle(id, n => n with { Color = NoteStyle.ClampColor(color) });

    public void SetSize(long id, NoteTextSize size) => Restyle(id, n => n with { Size = size });

    /// <summary>Puts the notes in this order; ids left out keep their order after it. Nothing is written when nothing moves.</summary>
    public void Reorder(IReadOnlyList<long> ids)
    {
        var known = ids.Where(id => IndexOf(id) >= 0).Distinct().ToList();
        var order = known.Concat(_notes.Select(n => n.Id).Where(id => !known.Contains(id))).ToList();
        if (order.SequenceEqual(_notes.Select(n => n.Id))) return;
        // The store numbers only the ids it gets, so it always gets every note.
        _store?.Reorder(order);
        var byId = _notes.ToDictionary(n => n.Id);
        _notes.Clear();
        for (var i = 0; i < order.Count; i++) _notes.Add(byId[order[i]] with { SortOrder = i });
        Changed?.Invoke();
    }

    public void Delete(long id)
    {
        Flush();
        var index = IndexOf(id);
        if (index < 0) return;
        _store?.Delete(id);
        _notes.RemoveAt(index);
        Changed?.Invoke();
    }

    /// <summary>The editor closed: what waits is written, and a note left empty is dropped. True when it was dropped.</summary>
    public bool Close(long id)
    {
        Flush();
        var index = IndexOf(id);
        if (index < 0 || !NoteSession.Discards(_notes[index].Body)) return false;
        Delete(id);
        return true;
    }

    /// <summary>Writes every note still waiting for its typing pause.</summary>
    public void Flush()
    {
        _pause.Stop();
        var waiting = _session.Take();
        if (waiting.Count == 0) return;
        foreach (var (id, _) in waiting)
        {
            var index = IndexOf(id);
            if (index >= 0) _store?.Update(_notes[index]);
        }
        _savedAt = _clock();
        SaveState?.Invoke(false, _savedAt);
    }

    /// <summary>Colour and size are written at once, with what was typed so far; the edit time stays.</summary>
    private void Restyle(long id, Func<Note, Note> change)
    {
        var index = IndexOf(id);
        if (index < 0) return;
        var changed = change(_notes[index]);
        if (changed == _notes[index]) return;
        _notes[index] = changed;
        _store?.Update(changed);
        Changed?.Invoke();
    }

    private int IndexOf(long id) => _notes.FindIndex(n => n.Id == id);
}
