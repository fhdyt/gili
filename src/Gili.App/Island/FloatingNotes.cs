using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Gili.Core.Mascot;
using Gili.Core.Notes;
using Gili.Platform.Shell;
using Microsoft.Win32;

namespace Gili.App.Island;

/// <summary>
/// The floating notes (F-TOOL-12): one <see cref="FloatingNoteWindow"/> per note that floats, made when it leaves the
/// island or when Gili starts, and none for notes in the island. Keeps them inside the work area when it changes,
/// hides them while an app is fullscreen, and flies them home on Back to island. Runs on the UI thread.
/// </summary>
public sealed class FloatingNotes
{
    private readonly NoteService _notes;
    private readonly IslandWindow _island;
    private readonly Dictionary<long, FloatingNoteWindow> _windows = [];
    private readonly HashSet<long> _landing = [];
    private bool _fullscreen;
    private bool _keeping;

    public FloatingNotes(NoteService notes, IslandWindow island)
    {
        _notes = notes;
        _island = island;
        notes.Floated += OnFloated;
        notes.Changed += Restyle;
        notes.Edited += note =>
        {
            if (_windows.TryGetValue(note.Id, out var window)) window.Show(note);
        };
        // The bar's strip, the Windows taskbar, the resolution or the DPI changed: every note moves back inside.
        SystemParameters.StaticPropertyChanged += OnSystemChanged;
        SystemEvents.DisplaySettingsChanged += (_, _) => KeepInsideSoon();
    }

    /// <summary>
    /// Gili's eyes should follow this point (screen DIPs) for this long (PRD 6.3 "Arah pandang"): a note moved by its
    /// header or flying home, until <see cref="WatchEnded"/>, or one just let go outside the island for 1.5 s.
    /// </summary>
    public event Action<Func<Point>, TimeSpan>? WatchRequested;

    public event Action? WatchEnded;

    /// <summary>A note came home into the closed island (Gili is happy for a moment).</summary>
    public event Action? CameHome;

    /// <summary>The windows of notes that floated when Gili closed, where they were, moved inside the work area.</summary>
    public void Restore()
    {
        var work = WorkArea();
        foreach (var note in _notes.Notes.Where(n => n.Float is not null).ToList())
        {
            var rect = NoteFloat.Clamp(note.Float!.Value, work);
            Open(note, rect);
            _notes.MoveFloat(note.Id, rect);
        }
    }

    /// <summary>
    /// Float on top: the note leaves the island at the top right of the work area (each further one 24 px in) and takes
    /// the keyboard once the island has closed. False when six notes float already.
    /// </summary>
    public bool FloatOnTop(long id) => _notes.Float(id, NoteFloat.Default(_notes.FloatingCount, WorkArea()), fromButton: true);

    /// <summary>Whether another note may leave the island; at six the Notes page says so instead (PRD 6.3 "Batas").</summary>
    public bool CanFloatAnother => NoteFloat.CanFloat(_notes.FloatingCount);

    /// <summary>
    /// A dragged note was let go outside the island and is growing into its window at <paramref name="rect"/>: it has
    /// left the island from now on, but its window comes only with <see cref="FinishLanding"/>. False at the limit.
    /// </summary>
    public bool BeginLanding(long id, NoteRect rect)
    {
        _landing.Add(id);
        if (_notes.Float(id, rect, fromButton: false))
        {
            // The island closes, and Gili looks at the note for 1.5 s.
            var inside = NoteFloat.Clamp(rect, WorkArea());
            WatchRequested?.Invoke(() => new Point(inside.X + inside.Width / 2, inside.Y + inside.Height / 2), GiliRules.WatchLanded);
            return true;
        }
        _landing.Remove(id);
        return false;
    }

    /// <summary>The dragged note has grown: its window takes over in the same place, springs inside the work area, and takes the keyboard.</summary>
    public FloatingNoteWindow? FinishLanding(long id)
    {
        if (!_landing.Remove(id) || _notes.Notes.FirstOrDefault(n => n.Id == id) is not { Float: { } rect } note) return null;
        var window = Open(note, rect);
        window.ArriveHeld();
        window.SettleInside();
        window.FocusText();
        return window;
    }

    /// <summary>Fullscreen hides every floating note, like the bar, and they come back after it (PRD 6.3).</summary>
    public void SetFullscreen(bool fullscreen)
    {
        _fullscreen = fullscreen;
        foreach (var window in _windows.Values) window.SetHidden(fullscreen);
    }

    /// <summary>The primary work area in DIPs (plan 2h decision 4).</summary>
    public NoteRect WorkArea()
    {
        var (left, top, right, bottom) = WindowStyles.PrimaryWorkArea();
        var dpi = VisualTreeHelper.GetDpi(_island);
        return new NoteRect(left / dpi.DpiScaleX, top / dpi.DpiScaleY, (right - left) / dpi.DpiScaleX, (bottom - top) / dpi.DpiScaleY);
    }

    private void OnFloated(Note note, bool fromButton)
    {
        // A dragged note still growing gets its window once it has grown.
        if (_landing.Contains(note.Id)) return;
        var window = Open(note, note.Float!.Value);
        if (fromButton) window.Born();
        else window.SettleInside();
        // After the island has closed and handed the keyboard back, so the note keeps it.
        window.Dispatcher.BeginInvoke(window.FocusText, DispatcherPriority.Input);
    }

    private FloatingNoteWindow Open(Note note, NoteRect rect)
    {
        var window = new FloatingNoteWindow(note, _notes, WorkArea);
        window.BackRequested += w => _ = ReturnAsync(w);
        window.Moving += (w, moving) => Follow(w, moving);
        _windows[note.Id] = window;
        window.ShowAt(rect);
        if (_fullscreen) window.SetHidden(true);
        return window;
    }

    /// <summary>
    /// Back to island (PRD 6.3, mockup returnFloat): into its card's slot when the Notes page is open, otherwise into a
    /// black pill at the island's middle, which then bulges once. The note keeps its place in the order; an empty one
    /// is dropped and only vanishes into the island.
    /// </summary>
    private async Task ReturnAsync(FloatingNoteWindow window)
    {
        if (!_windows.Remove(window.Id)) return;
        var intoGrid = _island.ShowsNotes;
        if (intoGrid) _island.ExpectNote(window.Id);
        var dropped = _notes.Return(window.Id);
        var slot = intoGrid && !dropped ? _island.NoteSlot(window.Id) : null;
        if (intoGrid && slot is null) _island.ReleaseNote(window.Id);
        if (!SystemParameters.ClientAreaAnimation)
        {
            await window.FadeOutAsync();
            window.Close();
            if (slot is not null) _island.ReleaseNote(window.Id);
            else CameHomeToClosedIsland();
            return;
        }
        NoteRect to;
        Color fill;
        double radius;
        if (slot is { } device)
        {
            var dpi = VisualTreeHelper.GetDpi(_island);
            to = new NoteRect(device.X / dpi.DpiScaleX, device.Y / dpi.DpiScaleY, device.Width / dpi.DpiScaleX, device.Height / dpi.DpiScaleY);
            fill = window.Fill;
            radius = NoteFloat.Corner;
        }
        else
        {
            // A black pill as tall as the compact island, at its middle, where the note disappears.
            var island = _island.IslandBounds();
            var h = Math.Min(island.Height, _island.CompactHeight);
            var w = Math.Min(island.Width, h * 3);
            to = new NoteRect(island.X + island.Width / 2 - w / 2, island.Y, w, h);
            fill = Colors.Black;
            radius = h / 2;
        }
        // The flight takes over from the note in the same place; the note goes once the flight is on screen.
        var flight = new NoteFlightWindow(window.Bounds, to, window.Fill, fill, radius, vanish: slot is null, window.Snapshot());
        flight.ShowStill();
        Rendered(window.Close);
        WatchRequested?.Invoke(() => Centre(flight.Current), TimeSpan.FromHours(1));
        await flight.FlyAsync();
        WatchEnded?.Invoke();
        if (slot is not null) _island.ReleaseNote(window.Id);
        else CameHomeToClosedIsland();
        // The card (or the island) is drawn in this frame; the flight goes once it is on screen, so nothing blinks.
        Rendered(flight.Close);
    }

    /// <summary>The closed island bulges once as the note arrives, and Gili is happy (PRD 6.3).</summary>
    private void CameHomeToClosedIsland()
    {
        if (_island.IsOpen) return;
        _island.Gulp();
        CameHome?.Invoke();
    }

    /// <summary>A note moved by its header is watched until it stops (an hour at most, in case a stop is never heard).</summary>
    private void Follow(FloatingNoteWindow window, bool moving)
    {
        if (!moving)
        {
            WatchEnded?.Invoke();
            return;
        }
        WatchRequested?.Invoke(() => Centre(window.Bounds), TimeSpan.FromHours(1));
    }

    private static Point Centre(NoteRect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    /// <summary>A note's colour or size changed (Ctrl+= in its window, or a restyle in the island).</summary>
    private void Restyle()
    {
        foreach (var note in _notes.Notes)
            if (_windows.TryGetValue(note.Id, out var window)) window.Show(note);
    }

    private void OnSystemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea)) KeepInsideSoon();
    }

    /// <summary>Once the change has settled (an AppBar strip moves in several steps), every note is moved inside and written.</summary>
    private void KeepInsideSoon()
    {
        if (_keeping) return;
        _keeping = true;
        _island.Dispatcher.BeginInvoke(() =>
        {
            _keeping = false;
            var work = WorkArea();
            foreach (var window in _windows.Values)
            {
                var inside = NoteFloat.Clamp(window.Bounds, work);
                window.Place(inside);
                _notes.MoveFloat(window.Id, inside);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>Runs once the next frame has been drawn.</summary>
    private static void Rendered(Action action)
    {
        var first = TimeSpan.MinValue;
        EventHandler? tick = null;
        tick = (_, e) =>
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (first == TimeSpan.MinValue) first = now;
            if (now == first) return;
            CompositionTarget.Rendering -= tick;
            action();
        };
        CompositionTarget.Rendering += tick;
    }
}
