using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Gili.Core.Layout;
using Gili.Core.Motion;
using Gili.Core.Notes;

namespace Gili.App.Island;

/// <summary>
/// The Notes page (PRD 6.3 "Halaman Notes", mockup openEditor/shutEditor): a Keep-style grid, and one editor that is
/// the opened card grown in place. The editor's frame (x, y, width, height, radius) follows springs stepped once per
/// rendered frame, only while it moves, so opening again while it shrinks carries on from what is on screen.
/// </summary>
public partial class NotesPage : UserControl
{
    /// <summary>The search row and the 10 px under it, above the grid.</summary>
    private const double TopRow = 42;

    private static readonly Brush AccentDot = Frozen(0x30, 0xD1, 0x58);
    private static readonly Brush BusyDot = Frozen(0xFF, 0x9F, 0x0A);

    private readonly NotesViewModel _vm;
    private readonly Dictionary<long, NoteCardView> _cards = [];
    private readonly Spring _x = new(0);
    private readonly Spring _y = new(0);
    private readonly Spring _w = new(0);
    private readonly Spring _h = new(0);
    private readonly Spring _r = new(12);
    private readonly Spring[] _frame;
    private readonly RectangleGeometry _clip = new();
    private readonly SolidColorBrush _editorFill = new(Color.FromRgb(0x1C, 0x1C, 0x1E));
    private readonly List<ToggleButton> _swatches = [];
    private readonly List<RadioButton> _sizes = [];
    private TimeSpan _lastFrame;
    private bool _animating;
    // The editor shows (open, or still shrinking), and the card it stands for, which stays hidden meanwhile.
    private bool _live;
    private bool _shutting;
    private long? _source;
    private bool _sourceIsNew;
    private NoteRect _from;
    private GrowX _growX;
    private GrowY _growY;
    private int _growing;

    public NotesPage(NotesViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _frame = [_x, _y, _w, _h, _r];
        Editor.Clip = _clip;
        Editor.Background = _editorFill;
        EditorShadow.Shadows = SoftShadow.NoteEditor;
        BuildSwatches();
        BuildSizes();
        vm.CardsChanged += SyncCards;
        vm.CardChanged += ShowCard;
        vm.EditorClosed += Shut;
        vm.PropertyChanged += OnChanged;
        SyncCards();
        ShowSize();
        ShowColor(animate: false);
        Placeholder.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The page's height is its content: the top row, then the cards or the open editor, whichever is taller, so the
    /// island grows first when a note opens (mockup pageNeed).
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        var size = base.MeasureOverride(constraint);
        if (!_vm.ReservesEditor) return size;
        var need = Math.Max(size.Height, Wrap.Margin.Top + TopRow + NoteGrid.EditorHeight + Wrap.Margin.Bottom);
        return new Size(size.Width, double.IsInfinity(constraint.Height) ? need : Math.Min(need, constraint.Height));
    }

    /// <summary>
    /// Keys the page answers before the island does (plan 2f decision 11): Esc closes the editor and then the island,
    /// Esc in the search clears it and then closes the island, arrows in the size picker change the size, and
    /// Ctrl+= / Ctrl+- / Ctrl+0 in the note text step it.
    /// </summary>
    public bool HandleKey(KeyEventArgs e)
    {
        var source = e.OriginalSource as Visual;
        if (e.Key == Key.Escape)
        {
            if (_vm.IsEditing)
            {
                var id = _vm.EditingId;
                _vm.CloseEditor();
                // An emptied note is gone with its card; New note takes the focus, so the next Esc closes the island.
                if (id is { } open && _cards.TryGetValue(open, out var card)) card.Focus();
                else NewNoteButton.Focus();
                return true;
            }
            if (!ReferenceEquals(source, SearchBox)) return false;
            if (SearchBox.Text.Length > 0) _vm.Search = "";
            else _vm.CloseIsland();
            return true;
        }
        if (!_vm.IsEditing || source is null || !Editor.IsAncestorOf(source)) return false;
        if (ReferenceEquals(source, EditorText))
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.Control) return false;
            int? step = e.Key switch
            {
                Key.OemPlus or Key.Add => 1,
                Key.OemMinus or Key.Subtract => -1,
                Key.D0 or Key.NumPad0 => 0,
                _ => null,
            };
            if (step is not { } delta) return false;
            _vm.StepSize(delta);
            return true;
        }
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return false;
        // The editor keeps its arrows, so they never switch pages under it; in the size picker they pick a size.
        if (SizePicker.IsAncestorOf(source))
        {
            _vm.SetSize(NoteStyle.Step(_vm.Size, e.Key is Key.Right or Key.Down ? 1 : -1));
            _sizes[(int)_vm.Size].Focus();
        }
        return true;
    }

    /// <summary>The cards follow the visible notes: the same view per note, so a card that moves slides.</summary>
    private void SyncCards()
    {
        var visible = _vm.Visible;
        var ids = visible.Select(n => n.Id).ToHashSet();
        foreach (var gone in _cards.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            Cards.Children.Remove(_cards[gone]);
            _cards.Remove(gone);
        }
        for (var i = 0; i < visible.Count; i++)
        {
            var note = visible[i];
            if (_cards.TryGetValue(note.Id, out var card))
            {
                card.Show(note, _vm.Edited(note));
            }
            else
            {
                card = new NoteCardView(note, _vm.Edited(note));
                card.Opening += OnCardOpening;
                _cards[note.Id] = card;
                Dim(card, animate: false);
            }
            if (i < Cards.Children.Count && ReferenceEquals(Cards.Children[i], card)) continue;
            Cards.Children.Remove(card);
            Cards.Children.Insert(i, card);
        }
    }

    /// <summary>A keystroke in the editor changes only its own card.</summary>
    private void ShowCard(Note note)
    {
        if (_cards.TryGetValue(note.Id, out var card)) card.Show(note, _vm.Edited(note));
    }

    private void OnCardOpening(NoteCardView card)
    {
        if (!_vm.IsEditing) Open(card.Id, isNew: false);
    }

    private void OnNewNote(object sender, RoutedEventArgs e)
    {
        var id = _vm.NewNote();
        Scroller.ScrollToTop();
        Open(id, isNew: true);
    }

    /// <summary>
    /// Opens a note: the island is asked for the editor's room first, and once the page has its final height the card
    /// grows into the editor (mockup openEditor: layout(), then edTarget).
    /// </summary>
    private void Open(long id, bool isNew)
    {
        _vm.Open(id);
        if (!_vm.IsEditing) return;
        _source = id;
        _sourceIsNew = isNew;
        // Its card hides and the others start to dim now, before the editor grows, so no frame shows them as they were.
        DimAll(animate: true);
        ShowColor(animate: _live);
        Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        Reserve(true);
        var token = ++_growing;
        // The island's own relayout for the new height is queued at the same priority, so it runs first.
        Dispatcher.InvokeAsync(() => Grow(token), DispatcherPriority.Loaded);
    }

    private void Grow(int token)
    {
        if (token != _growing || !_vm.IsEditing) return;
        UpdateLayout();
        var grid = VisibleGrid();
        if (_sourceIsNew) _from = NoteGrid.NewCard(grid, Cards.ColumnWidth);
        else if (SlotRect(_source) is { } slot) _from = slot;
        var (frame, growX, growY) = NoteGrid.Editor(_from, grid);
        _growX = growX;
        _growY = growY;
        // Laid out once at the final size, so the text never reflows while the frame grows.
        EditorContent.Width = frame.Width;
        EditorContent.Height = frame.Height;
        var animated = SystemParameters.ClientAreaAnimation;
        _shutting = false;
        if (!_live)
        {
            _live = true;
            SnapFrame(_from, 12);
            EditorLayer.Visibility = Visibility.Visible;
            EditorLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(animated ? 120 : 100)));
        }
        else
        {
            // A close still in flight is taken over from where the editor is on screen.
            EditorLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Ms(animated ? 120 : 100)));
        }
        EditorContent.BeginAnimation(OpacityProperty, animated
            ? new DoubleAnimation(1, Ms(180)) { BeginTime = Ms(140) }
            : new DoubleAnimation(1, Ms(100)));
        if (animated) MoveFrame(frame, 16, SpringParameters.Open);
        else SnapFrame(frame, 16);
        Dispatcher.InvokeAsync(() =>
        {
            if (!_vm.IsEditing) return;
            EditorText.Focus();
            EditorText.CaretIndex = EditorText.Text.Length;
        }, DispatcherPriority.Input);
    }

    /// <summary>
    /// The editor shrinks back (mockup shutEditor): to the card's slot, or where it grew from when the note is dropped,
    /// fading on the way when the note is gone; the card takes its place once it lands. Closing with the island goes at once.
    /// </summary>
    private void Shut(EditorClose close)
    {
        _growing++;
        Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        if (!_live || close.Instant)
        {
            FinishShut();
            return;
        }
        var target = (close.ToSlot ? SlotRect(close.Id) : null) ?? _from;
        _shutting = true;
        DimAll(animate: true);
        if (!SystemParameters.ClientAreaAnimation)
        {
            // Without Windows animations the editor and its card cross-fade in 100 ms.
            SnapFrame(target, 12);
            if (_source is { } id && _cards.TryGetValue(id, out var card)) card.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Ms(100)));
            var fade = new DoubleAnimation(0, Ms(100));
            fade.Completed += (_, _) =>
            {
                if (_shutting) FinishShut();
            };
            EditorLayer.BeginAnimation(OpacityProperty, fade);
            return;
        }
        EditorContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Ms(120)));
        if (close.Fade) EditorLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Ms(160)) { BeginTime = Ms(160) });
        MoveFrame(target, 12, SpringParameters.Close);
    }

    /// <summary>The landed editor is swapped for its card in the same frame, and the page lets go of the editor's room.</summary>
    private void FinishShut()
    {
        _shutting = false;
        _live = false;
        foreach (var spring in _frame) spring.SnapTo(spring.Target);
        EditorLayer.BeginAnimation(OpacityProperty, null);
        EditorLayer.Opacity = 0;
        EditorLayer.Visibility = Visibility.Collapsed;
        EditorContent.BeginAnimation(OpacityProperty, null);
        EditorContent.Opacity = 0;
        _source = null;
        DimAll(animate: false);
        Reserve(false);
    }

    /// <summary>While a note is open the other cards step back to 30% in 220 ms; its own card is the editor, so it is hidden until it lands.</summary>
    private void DimAll(bool animate)
    {
        foreach (var card in _cards.Values) Dim(card, animate);
    }

    private void Dim(NoteCardView card, bool animate)
    {
        var hidden = card.Id == _source;
        card.IsHitTestVisible = !hidden;
        var target = hidden ? 0 : _vm.IsEditing ? 0.3 : 1;
        if (hidden || !animate)
        {
            card.BeginAnimation(OpacityProperty, null);
            card.Opacity = target;
            return;
        }
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(target, Ms(SystemParameters.ClientAreaAnimation ? 220 : 100)));
    }

    private void Reserve(bool on)
    {
        if (_vm.ReservesEditor == on) return;
        InvalidateMeasure();
        _vm.ReservesEditor = on;
    }

    /// <summary>The grid's visible area in the page's coordinates: the editor stays inside it.</summary>
    private NoteRect VisibleGrid()
    {
        var origin = Scroller.TranslatePoint(new Point(0, 0), Wrap);
        return new NoteRect(origin.X, origin.Y, Scroller.ActualWidth, Scroller.ActualHeight);
    }

    /// <summary>Where a card rests in the grid (not mid-slide), scrolled, in the page's coordinates.</summary>
    private NoteRect? SlotRect(long? id)
    {
        if (id is not { } key || !_cards.TryGetValue(key, out var card) || Cards.SlotOf(card) is not { } slot) return null;
        var origin = Cards.TranslatePoint(new Point(slot.X, slot.Y), Wrap);
        return new NoteRect(origin.X, origin.Y, slot.Width, slot.Height);
    }

    private void SnapFrame(NoteRect rect, double radius)
    {
        _x.SnapTo(rect.X);
        _y.SnapTo(rect.Y);
        _w.SnapTo(rect.Width);
        _h.SnapTo(rect.Height);
        _r.SnapTo(radius);
        ApplyFrame();
    }

    private void MoveFrame(NoteRect rect, double radius, SpringParameters parameters)
    {
        foreach (var spring in _frame) spring.Parameters = parameters;
        _x.Target = rect.X;
        _y.Target = rect.Y;
        _w.Target = rect.Width;
        _h.Target = rect.Height;
        _r.Target = radius;
        if (_animating) return;
        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastFrame) return; // Rendering can be raised more than once per frame.
        var seconds = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        foreach (var spring in _frame) spring.Step(seconds);
        ApplyFrame();
        // Within half a pixel the editor has landed: its card takes over in this frame.
        if (_shutting && _frame.Take(4).All(s => Math.Abs(s.Value - s.Target) < 0.5)) FinishShut();
        if (!_frame.All(s => s.IsAtRest)) return;
        CompositionTarget.Rendering -= OnFrame;
        _animating = false;
    }

    private void ApplyFrame()
    {
        var w = Math.Max(0, _w.Value);
        var h = Math.Max(0, _h.Value);
        var r = Math.Clamp(_r.Value, 0, Math.Min(w, h) / 2);
        Canvas.SetLeft(Editor, _x.Value);
        Canvas.SetTop(Editor, _y.Value);
        Editor.Width = w;
        Editor.Height = h;
        Editor.CornerRadius = new CornerRadius(r);
        EditorEdge.CornerRadius = new CornerRadius(r);
        _clip.Rect = new Rect(0, 0, w, h);
        _clip.RadiusX = r;
        _clip.RadiusY = r;
        Canvas.SetLeft(EditorShadow, _x.Value);
        Canvas.SetTop(EditorShadow, _y.Value);
        EditorShadow.Width = w;
        EditorShadow.Height = h;
        EditorShadow.CornerRadius = r;
        // The content stays pinned to the corner the frame grows from.
        var width = double.IsNaN(EditorContent.Width) ? 0 : EditorContent.Width;
        var height = double.IsNaN(EditorContent.Height) ? 0 : EditorContent.Height;
        Canvas.SetLeft(EditorContent, _growX switch
        {
            GrowX.Left => 0,
            GrowX.Right => w - width,
            _ => (w - width) / 2,
        });
        Canvas.SetTop(EditorContent, _growY == GrowY.Down ? 0 : h - height);
    }

    /// <summary>Eight colours as toggle buttons (aria-pressed): "Color: Red", tooltip "Red".</summary>
    private void BuildSwatches()
    {
        var style = (Style)FindResource("Swatch");
        for (var i = 0; i < NoteStyle.Colors.Count; i++)
        {
            var color = i;
            var name = NoteStyle.Colors[i].Name;
            var swatch = new ToggleButton
            {
                Style = style,
                Background = NoteCardView.FillOf(i),
                ToolTip = name,
                Margin = new Thickness(i == 0 ? 0 : 7, 0, 0, 0),
            };
            AutomationProperties.SetName(swatch, $"Color: {name}");
            swatch.Click += (_, _) =>
            {
                _vm.SetColor(color);
                ShowColor(animate: true);
            };
            _swatches.Add(swatch);
            Swatches.Children.Add(swatch);
        }
    }

    /// <summary>Four sizes as a radio group, each an "A" at its own size; only the chosen one is a tab stop.</summary>
    private void BuildSizes()
    {
        var style = (Style)FindResource("SizeOption");
        foreach (var size in Enum.GetValues<NoteTextSize>())
        {
            var look = NoteStyle.Size(size);
            var option = new RadioButton
            {
                Style = style,
                ToolTip = look.Name,
                Content = new TextBlock { Text = "A", FontSize = look.Glyph, LineHeight = look.Glyph, LineStackingStrategy = LineStackingStrategy.BlockLineHeight },
            };
            AutomationProperties.SetName(option, look.Name);
            option.Click += (_, _) => _vm.SetSize(size);
            _sizes.Add(option);
            SizePicker.Children.Add(option);
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NotesViewModel.Size):
                ShowSize();
                break;
            case nameof(NotesViewModel.Color):
                ShowColor(animate: _live);
                break;
            case nameof(NotesViewModel.Body):
                Placeholder.Visibility = _vm.Body.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(NotesViewModel.IsSaving):
                ShowSaving();
                break;
        }
    }

    /// <summary>The size applies to the text at once, without motion; line height 1.6.</summary>
    private void ShowSize()
    {
        var font = NoteStyle.Size(_vm.Size).Editor;
        EditorText.FontSize = font;
        TextBlock.SetLineHeight(EditorText, font * 1.6);
        Placeholder.FontSize = font;
        Placeholder.LineHeight = font * 1.6;
        for (var i = 0; i < _sizes.Count; i++)
        {
            var chosen = i == (int)_vm.Size;
            _sizes[i].IsChecked = chosen;
            _sizes[i].IsTabStop = chosen;
        }
    }

    /// <summary>The editor takes the note's colour (200 ms while open) and the chosen swatch shows its ring.</summary>
    private void ShowColor(bool animate)
    {
        var rgb = NoteStyle.Colors[NoteStyle.ClampColor(_vm.Color)].Rgb;
        var color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        _editorFill.BeginAnimation(SolidColorBrush.ColorProperty, animate && SystemParameters.ClientAreaAnimation
            ? new ColorAnimation(color, Ms(200))
            : null);
        if (!animate) _editorFill.Color = color;
        for (var i = 0; i < _swatches.Count; i++) _swatches[i].IsChecked = i == _vm.Color;
    }

    /// <summary>Green dot when saved; orange and blinking while a save waits (mockup .nsave.busy).</summary>
    private void ShowSaving()
    {
        SaveDot.Fill = _vm.IsSaving ? BusyDot : AccentDot;
        SaveDot.BeginAnimation(OpacityProperty, _vm.IsSaving
            ? new DoubleAnimation(1, 0.3, Ms(600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } }
            : null);
    }

    /// <summary>While a note is open, a press anywhere on the dimmed grid is Done.</summary>
    private void OnGridPress(object sender, MouseButtonEventArgs e)
    {
        if (!_vm.IsEditing) return;
        _vm.CloseEditor();
        e.Handled = true;
    }

    /// <summary>The grid holds still under an open note (mockup .nwrap.editing .ngrid overflow hidden).</summary>
    private void OnGridWheel(object sender, MouseWheelEventArgs e)
    {
        if (_live) e.Handled = true;
    }

    /// <summary>The wheel over the open note scrolls its text, never the pages behind it.</summary>
    private void OnEditorWheel(object sender, MouseWheelEventArgs e) => e.Handled = true;

    /// <summary>A new search lays the cards out at once, like the mockup's renderNotes.</summary>
    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Cards.SnapNext = true;

    private void OnGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged) _vm.NotifyContentChanged();
    }

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
