using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Clipboard;
using Gili.Core.Island;

namespace Gili.App.Island;

/// <summary>
/// The Clipboard page (PRD 6.3 "Halaman Clipboard", F-ISL-28, mockup renderClipPage): a search field and Clear all, then
/// the history of text, images and files, newest first (F-ISL-32). Picking a row pastes it where the user was typing;
/// × removes one; Clear all asks once. The rows are built only while the page shows (PRD 8.1).
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
                // Copied files that are all gone since leave first (F-ISL-32).
                if (!island.ClipboardHistory.PruneFiles(p => File.Exists(p) || Directory.Exists(p))) Refresh();
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
            Rows.Add(new ClipboardRowViewModel(entry, now, () => _island.Paste(entry.Id), () => _island.ClipboardHistory.Remove(entry.Id), _island.ClipboardImages));
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

/// <summary>
/// One copy: the text on one line (an image's name and size, a file's name or "3 files"), the app and how long ago, and
/// the tooltip. Images show a thumbnail and files their icon at the start of the row (F-ISL-32).
/// </summary>
public sealed partial class ClipboardRowViewModel : ObservableObject
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    private readonly ClipboardEntry _entry;
    private readonly ClipboardImages? _images;
    private ImageSource? _thumb;
    private bool _thumbRead;

    [ObservableProperty] private bool _isSelected;

    public ClipboardRowViewModel(ClipboardEntry entry, DateTimeOffset now, Action paste, Action remove, ClipboardImages? images = null)
    {
        _entry = entry;
        _images = images;
        Id = entry.Id;
        Text = ClipboardText.Line(entry);
        Detail = ClipboardText.Detail(entry, now);
        Tooltip = ClipboardText.Tooltip(entry);
        PasteCommand = new RelayCommand(paste);
        RemoveCommand = new RelayCommand(remove);
        if (entry.Kind == ClipKind.Files)
            Glyph = (Geometry)Application.Current.FindResource(entry.Files.Count > 1 ? "Icon.Files" : Directory.Exists(entry.Files[0]) ? "Icon.Folder" : "Icon.File");
    }

    public long Id { get; }

    /// <summary>Text rows have nothing before the text.</summary>
    public bool HasLead => _entry.Kind != ClipKind.Text;

    /// <summary>A thumbnail shows for an image and for one copied image file; other files show <see cref="Glyph"/>.</summary>
    public bool HasThumb => Thumb is not null;

    public bool HasGlyph => Glyph is not null && !HasThumb;

    public Geometry? Glyph { get; }

    /// <summary>Decoded small, the first time the row is drawn, so a closed page decodes nothing (PRD 8.1).</summary>
    public ImageSource? Thumb
    {
        get
        {
            if (_thumbRead) return _thumb;
            _thumbRead = true;
            _thumb = _entry.Kind switch
            {
                ClipKind.Image => _images?.Thumb(_entry.Text),
                ClipKind.Files when _entry.Files.Count == 1 && ImageExtensions.Contains(Path.GetExtension(_entry.Files[0]), StringComparer.OrdinalIgnoreCase)
                    => FileThumb(_entry.Files[0]),
                _ => null,
            };
            return _thumb;
        }
    }

    private static ImageSource? FileThumb(string path)
    {
        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = 80;
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    public string Text { get; }

    public string Detail { get; }

    public string Tooltip { get; }

    public IRelayCommand PasteCommand { get; }

    public IRelayCommand RemoveCommand { get; }
}
