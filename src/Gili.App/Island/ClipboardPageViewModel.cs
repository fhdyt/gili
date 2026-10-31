using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Clipboard;
using Gili.Core.Island;

namespace Gili.App.Island;

/// <summary>
/// The Clipboard page (PRD 6.3 "Halaman Clipboard", F-ISL-28, mockup renderClipPage): a search field and Clear all, then
/// the history, newest first. Picking a row pastes it where the user was typing; × removes one; Clear all asks once.
/// The rows are built only while the page shows (PRD 8.1).
/// </summary>
public sealed partial class ClipboardPageViewModel : ObservableObject
{
    private readonly IslandController _island;
    private readonly DispatcherTimer _confirm = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _shown;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private int _selected = -1;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _noMatches;
    [ObservableProperty] private bool _confirmingClear;

    public ClipboardPageViewModel(IslandController island)
    {
        _island = island;
        _confirm.Tick += (_, _) =>
        {
            _confirm.Stop();
            ConfirmingClear = false;
        };
        ClearCommand = new RelayCommand(Clear);
        island.ClipboardHistory.Changed += () =>
        {
            if (_shown) Refresh();
        };
        island.ViewChanged += view =>
        {
            var shown = view.Mode == IslandMode.Expanded && view.Page == IslandPage.Clipboard;
            if (shown == _shown) return;
            _shown = shown;
            // Each visit starts with an empty search and nothing highlighted (mockup openClipboard).
            if (shown)
            {
                Query = "";
                Selected = -1;
                Refresh();
            }
        };
        Refresh();
    }

    public ObservableCollection<ClipboardRowViewModel> Rows { get; } = [];

    public IRelayCommand ClearCommand { get; }

    public string ClearLabel => ConfirmingClear ? "Clear all?" : "Clear all";

    /// <summary>The list changed size, so the open island fits it again.</summary>
    public event Action? ContentChanged;

    partial void OnQueryChanged(string value)
    {
        // Typing highlights the first match, so Enter pastes it.
        Selected = string.IsNullOrWhiteSpace(value) ? -1 : 0;
        Refresh();
    }

    partial void OnConfirmingClearChanged(bool value) => OnPropertyChanged(nameof(ClearLabel));

    partial void OnSelectedChanged(int value)
    {
        for (var i = 0; i < Rows.Count; i++) Rows[i].IsSelected = i == value;
    }

    /// <summary>Ctrl+Alt+V highlights the first row, so Enter pastes it at once (mockup openClipboard(fromKeys)).</summary>
    public void SelectFirst() => Selected = Rows.Count > 0 ? 0 : -1;

    /// <summary>↑ and ↓ from the list or the search field.</summary>
    public void Move(int step)
    {
        if (Rows.Count == 0) return;
        Selected = Math.Clamp(Selected + step, 0, Rows.Count - 1);
    }

    /// <summary>Enter: paste the highlighted row.</summary>
    public bool PasteSelected()
    {
        if (Selected < 0 || Selected >= Rows.Count) return false;
        _island.Paste(Rows[Selected].Id);
        return true;
    }

    /// <summary>Delete: remove the highlighted row; the highlight stays on the row that takes its place.</summary>
    public bool RemoveSelected()
    {
        if (Selected < 0 || Selected >= Rows.Count) return false;
        var keep = Selected;
        _island.ClipboardHistory.Remove(Rows[Selected].Id);
        Selected = Math.Min(keep, Rows.Count - 1);
        return true;
    }

    private void Refresh()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = _island.ClipboardHistory.Search(Query);
        Rows.Clear();
        foreach (var entry in entries)
            Rows.Add(new ClipboardRowViewModel(entry, now, () => _island.Paste(entry.Id), () => _island.ClipboardHistory.Remove(entry.Id)));
        HasItems = _island.ClipboardHistory.Items.Count > 0;
        IsEmpty = !HasItems;
        NoMatches = HasItems && Rows.Count == 0;
        if (Selected >= Rows.Count) Selected = Rows.Count - 1;
        OnSelectedChanged(Selected);
        ContentChanged?.Invoke();
    }

    private void Clear()
    {
        if (!ConfirmingClear)
        {
            ConfirmingClear = true;
            _confirm.Start();
            return;
        }
        _confirm.Stop();
        ConfirmingClear = false;
        _island.ClipboardHistory.Clear();
    }
}

/// <summary>One copied text: the text on one line, the app and how long ago, and up to 400 characters as the tooltip.</summary>
public sealed partial class ClipboardRowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public ClipboardRowViewModel(ClipboardEntry entry, DateTimeOffset now, Action paste, Action remove)
    {
        Id = entry.Id;
        Text = ClipboardText.Line(entry.Text);
        Detail = ClipboardText.Detail(entry, now);
        Tooltip = ClipboardText.Tooltip(entry.Text);
        PasteCommand = new RelayCommand(paste);
        RemoveCommand = new RelayCommand(remove);
    }

    public long Id { get; }

    public string Text { get; }

    public string Detail { get; }

    public string Tooltip { get; }

    public IRelayCommand PasteCommand { get; }

    public IRelayCommand RemoveCommand { get; }
}
