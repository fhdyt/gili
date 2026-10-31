using System.Windows.Controls;
using System.Windows.Input;

namespace Gili.App.Island;

/// <summary>
/// The Clipboard page's keys (PRD 6.3): ↑/↓ move the highlight, also from the search field; Enter pastes it; Delete
/// removes it unless the search field has focus; Esc closes the island without pasting.
/// </summary>
public partial class ClipboardPage : UserControl
{
    private readonly ClipboardPageViewModel _vm;
    private readonly Action _close;

    public ClipboardPage(ClipboardPageViewModel vm, Action close)
    {
        _vm = vm;
        _close = close;
        InitializeComponent();
        DataContext = vm;
        PreviewKeyDown += OnKey;
    }

    /// <summary>Ctrl+Alt+V (Phase 4b): typing filters at once.</summary>
    public void FocusSearch()
    {
        Search.Focus();
        Keyboard.Focus(Search);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var inSearch = e.OriginalSource == Search;
        e.Handled = e.Key switch
        {
            Key.Down => Run(() => _vm.Move(1)),
            Key.Up => Run(() => _vm.Move(-1)),
            Key.Enter => _vm.PasteSelected(),
            Key.Delete when !inSearch => _vm.RemoveSelected(),
            Key.Escape when inSearch => Run(_close),
            _ => false,
        };
    }

    private static bool Run(Action action)
    {
        action();
        return true;
    }
}
