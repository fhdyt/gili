using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.Core.Notes;

namespace Gili.App.Island;

/// <summary>
/// One note in the grid: its title, up to 8 lines of the rest and when it was last edited, in the note's colour and
/// text size. A press and release on it, or Enter or Space while it has focus, opens it.
/// </summary>
public partial class NoteCardView : UserControl
{
    private static readonly Brush DefaultBody = Frozen(0xFF, 0xC7, 0xC7, 0xCC);
    private static readonly Brush DefaultTime = Frozen(0xFF, 0x8E, 0x8E, 0x93);
    private static readonly Brush TintedBody = Frozen(0xE0, 0xFF, 0xFF, 0xFF);
    private static readonly Brush TintedTime = Frozen(0xB8, 0xFF, 0xFF, 0xFF);
    private static readonly Brush[] Fills = [.. NoteStyle.Colors.Select(c => Frozen(0xFF, (byte)(c.Rgb >> 16), (byte)(c.Rgb >> 8), (byte)c.Rgb))];

    private bool _pressed;

    public NoteCardView(Note note, string edited)
    {
        InitializeComponent();
        Note = note;
        Show(note, edited);
    }

    public Note Note { get; private set; }

    public long Id => Note.Id;

    /// <summary>Asked to open: pressed and released on the card, or Enter or Space.</summary>
    public event Action<NoteCardView>? Opening;

    public static Brush FillOf(int color) => Fills[NoteStyle.ClampColor(color)];

    public void Show(Note note, string edited)
    {
        Note = note;
        var size = NoteStyle.Size(note.Size);
        var title = NoteText.Title(note.Body);
        var rest = NoteText.Rest(note.Body);
        var blank = title.Length == 0 && rest.Length == 0;
        Title.Text = title;
        Title.FontSize = size.CardTitle;
        Title.Visibility = title.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        // The title's 4 px meets the time's 8 px when there is no body; CSS keeps the larger.
        Title.Margin = new Thickness(0, 0, 0, rest.Length > 0 ? 4 : 0);
        Body.Text = blank ? NoteText.Empty : rest;
        Body.FontSize = size.CardBody;
        Body.LineHeight = size.CardBody * 1.45;
        Body.Visibility = rest.Length > 0 || blank ? Visibility.Visible : Visibility.Collapsed;
        Time.Text = edited;
        var tinted = note.Color > 0;
        Body.Foreground = tinted ? TintedBody : DefaultBody;
        Time.Foreground = tinted ? TintedTime : DefaultTime;
        Fill.Background = FillOf(note.Color);
        AutomationProperties.SetName(this, NoteText.Label(note.Body));
    }

    // Only the keyboard focuses a card (a click does not), so this is :focus-visible.
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Ring.Visibility = IsKeyboardFocused ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Ring.Visibility = IsKeyboardFocused ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        Hover.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _pressed = false;
        Hover.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
    }

    // Task 5 turns a press that moves 6 px into a drag; a press released in place opens the note.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _pressed = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_pressed) return;
        _pressed = false;
        Opening?.Invoke(this);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Enter or Key.Space) || Keyboard.Modifiers != ModifierKeys.None) return;
        Opening?.Invoke(this);
        e.Handled = true;
    }

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
