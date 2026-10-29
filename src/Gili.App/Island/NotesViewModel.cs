using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Layout;
using Gili.Core.Notes;

namespace Gili.App.Island;

/// <summary>How the open editor goes back: to its card's slot or where it grew from, fading when the note is gone.</summary>
public readonly record struct EditorClose(long Id, bool ToSlot, bool Fade, bool Instant);

/// <summary>
/// The Notes page (PRD 6.3 "Halaman Notes"): the cards that match the search in the user's order, and the one note
/// open in the editor with its colour, text size, save state and two-step Delete. The page owns the motion.
/// </summary>
public sealed partial class NotesViewModel : ObservableObject
{
    private readonly NoteService _notes;
    private readonly Func<bool> _use24Hour;
    private readonly Action _closeIsland;
    private readonly DispatcherTimer _confirm = new() { Interval = TimeSpan.FromSeconds(3) };
    private List<Note> _visible = [];
    private bool _loading;
    private bool _reservesEditor;

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _body = "";
    [ObservableProperty] private int _color;
    [ObservableProperty] private NoteTextSize _size = NoteStyle.Default;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _saveText = "Saved";

    /// <summary>A card is lifted or still landing: the island does not close on its own meanwhile.</summary>
    [ObservableProperty] private bool _isDragging;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    private bool _isConfirmingDelete;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyText = NoteText.NoNotes;

    public NotesViewModel(NoteService notes, Func<bool> use24Hour, Action closeIsland)
    {
        _notes = notes;
        _use24Hour = use24Hour;
        _closeIsland = closeIsland;
        _confirm.Tick += (_, _) => ResetDelete();
        notes.Changed += Refresh;
        notes.Edited += OnEdited;
        notes.SaveState += OnSaveState;
        Refresh();
    }

    /// <summary>The cards to show: every note matching the search, in the user's order.</summary>
    public IReadOnlyList<Note> Visible => _visible;

    /// <summary>The note in the editor; null while it is closed.</summary>
    public long? EditingId { get; private set; }

    /// <summary>A card lifts only while nothing is searched and no note is open (PRD 6.3 "Mengatur urutan").</summary>
    public bool CanLift => !IsEditing && NoteText.IsBlank(Search);

    public string DeleteText => IsConfirmingDelete ? "Click again to delete" : "Delete";

    /// <summary>The page height keeps the editor's 360 px while it is open or still shrinking (mockup pageNeed).</summary>
    public bool ReservesEditor
    {
        get => _reservesEditor;
        set
        {
            if (_reservesEditor == value) return;
            _reservesEditor = value;
            ContentChanged?.Invoke();
        }
    }

    /// <summary>Raised when the page needs a new height: the cards or the editor's room changed.</summary>
    public event Action? ContentChanged;

    /// <summary>The cards to show changed: which, their order, or how they look.</summary>
    public event Action? CardsChanged;

    /// <summary>One note's text changed while typing; only its card follows.</summary>
    public event Action<Note>? CardChanged;

    /// <summary>The editor closed; raised before the note is written or dropped, so its card is still in the grid.</summary>
    public event Action<EditorClose>? EditorClosed;

    public void NotifyContentChanged() => ContentChanged?.Invoke();

    public void Refresh()
    {
        _visible = [.. _notes.Notes.Where(n => NoteText.Matches(n.Body, Search))];
        IsEmpty = Visible.Count == 0;
        EmptyText = _notes.Notes.Count == 0 ? NoteText.NoNotes : NoteText.NoMatch;
        CardsChanged?.Invoke();
    }

    /// <summary>The time on a card: the clock today, then Yesterday, a weekday or a date.</summary>
    public string Edited(Note note) => NoteText.Edited(note.UpdatedAt, DateTimeOffset.UtcNow, TimeZoneInfo.Local, _use24Hour());

    /// <summary>Opens a note in the editor, "Saved" until the next key.</summary>
    public void Open(long id)
    {
        if (_notes.Notes.FirstOrDefault(n => n.Id == id) is not { } note) return;
        _notes.Flush();
        _loading = true;
        EditingId = id;
        Body = note.Body;
        Color = note.Color;
        Size = note.Size;
        _loading = false;
        ResetDelete();
        IsSaving = false;
        SaveText = "Saved";
        IsEditing = true;
    }

    /// <summary>New note: the search is cleared and an empty note is made on top; the page opens it.</summary>
    public long NewNote()
    {
        CloseEditor();
        Search = "";
        return _notes.Create().Id;
    }

    // The service keeps the text with \n line breaks; the TextBox keeps its own, so the caret never moves.
    partial void OnBodyChanged(string value)
    {
        if (!_loading && EditingId is { } id) _notes.Edit(id, value);
    }

    /// <summary>Typing keeps the card where it is, even when it no longer matches the search (mockup refreshCard).</summary>
    private void OnEdited(Note note)
    {
        var index = _visible.FindIndex(n => n.Id == note.Id);
        if (index < 0) return;
        _visible[index] = note;
        CardChanged?.Invoke(note);
    }

    /// <summary>Puts the cards in this order (a drag); the order is written once, and not at all when nothing moved.</summary>
    public void Reorder(IReadOnlyList<long> ids) => _notes.Reorder(ids);

    /// <summary>
    /// Alt + arrow: the note swaps places with the card next to it on screen (left and up are earlier), within the
    /// whole order, so notes hidden by a search keep theirs. False at either end.
    /// </summary>
    public bool Step(long id, int delta)
    {
        if (ListOrder.Neighbor([.. _visible.Select(n => n.Id)], id, delta) is not { } neighbor) return false;
        List<long> order = [.. _notes.Notes.Select(n => n.Id)];
        _notes.Reorder(ListOrder.Move(order, id, order.IndexOf(neighbor)));
        return true;
    }

    partial void OnSearchChanged(string value)
    {
        CloseEditor();
        Refresh();
    }

    [RelayCommand]
    private void Done() => CloseEditor();

    /// <summary>Done, Esc or a press on the dimmed grid: the note is written, or dropped when it is empty.</summary>
    public void CloseEditor() => Close(instant: false);

    /// <summary>The island closed with the editor open: written, closed without motion, and the grid shows next time (plan 2f decision 12).</summary>
    public void CloseEditorNow() => Close(instant: true);

    private void Close(bool instant)
    {
        if (!IsEditing || EditingId is not { } id) return;
        var empty = NoteSession.Discards(Body);
        Shut();
        EditorClosed?.Invoke(new EditorClose(id, ToSlot: !empty, Fade: empty, instant));
        _notes.Close(id);
    }

    /// <summary>Delete asks once ("Click again to delete" for 3 s); the editor then shrinks into the slot and fades.</summary>
    [RelayCommand]
    private void Delete()
    {
        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            _confirm.Start();
            return;
        }
        if (EditingId is not { } id) return;
        Shut();
        EditorClosed?.Invoke(new EditorClose(id, ToSlot: true, Fade: true, Instant: false));
        _notes.Delete(id);
    }

    public void SetColor(int color)
    {
        if (EditingId is not { } id) return;
        Color = NoteStyle.ClampColor(color);
        _notes.SetColor(id, Color);
    }

    public void SetSize(NoteTextSize size)
    {
        if (EditingId is not { } id) return;
        Size = size;
        _notes.SetSize(id, size);
    }

    /// <summary>Ctrl+= and Ctrl+- step the size, Ctrl+0 goes back to Medium.</summary>
    public void StepSize(int delta) => SetSize(delta == 0 ? NoteStyle.Default : NoteStyle.Step(Size, delta));

    public void CloseIsland() => _closeIsland();

    private void Shut()
    {
        ResetDelete();
        IsEditing = false;
        EditingId = null;
    }

    private void ResetDelete()
    {
        _confirm.Stop();
        IsConfirmingDelete = false;
    }

    private void OnSaveState(bool busy, DateTimeOffset? savedAt)
    {
        IsSaving = busy;
        SaveText = busy ? "Saving…" : savedAt is { } at ? NoteText.Saved(at, TimeZoneInfo.Local, _use24Hour()) : "Saved";
    }
}
