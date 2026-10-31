using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Shelf;

namespace Gili.App.Island;

/// <summary>
/// The Shelf page (PRD 6.3 page 8, mockup <c>renderShelf</c>): the file count and total size on the left, and up to
/// eight tiles in a 4 × 2 grid.
/// </summary>
public sealed partial class ShelfViewModel : ObservableObject
{
    private readonly ShelfService _shelf;

    [ObservableProperty] private string _countText = "0";
    [ObservableProperty] private string _summary = "files on the shelf";
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private IReadOnlyList<ShelfTileViewModel> _tiles = [];

    public ShelfViewModel(ShelfService shelf)
    {
        _shelf = shelf;
        ClearCommand = new RelayCommand(shelf.Clear);
        shelf.Changed += Refresh;
        Refresh();
    }

    public IRelayCommand ClearCommand { get; }

    public ShelfService Shelf => _shelf;

    /// <summary>The page's content changed size, so the open island fits it again.</summary>
    public event Action? ContentChanged;

    private void Refresh()
    {
        CountText = _shelf.FileCount.ToString(CultureInfo.InvariantCulture);
        Summary = ShelfText.Summary(_shelf.FileCount, _shelf.TotalBytes);
        HasItems = _shelf.Items.Count > 0;
        Tiles = [.. _shelf.Items.Select(i => new ShelfTileViewModel(i, _shelf))];
        ContentChanged?.Invoke();
    }
}

/// <summary>One tile: the file type's icon with its badge, the name, and the hover text with every file's size.</summary>
public sealed partial class ShelfTileViewModel : ObservableObject
{
    private readonly ShelfService _shelf;

    [ObservableProperty] private bool _gone;

    public ShelfTileViewModel(ShelfItem item, ShelfService shelf)
    {
        Item = item;
        _shelf = shelf;
        var facts = item.Paths.Select(p => (p, shelf.Facts(p))).ToList();
        var folder = !item.IsBundle && facts[0].Item2 is { IsFolder: true };
        (BadgeText, var color) = ShelfText.Badge(item, folder);
        BadgeBrush = color.Length == 0 ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromInvariantString(color)!;
        if (!BadgeBrush.IsFrozen) BadgeBrush.Freeze();
        Glyph = (Geometry)Application.Current.FindResource(item.IsBundle ? "Icon.Files" : folder ? "Icon.Folder" : "Icon.File");
        // The demo's file isn't on disk, so a single file falls back to the size kept when it was dropped.
        Tooltip = item.IsBundle || facts[0].Item2 is not null
            ? ShelfText.Tooltip(facts)
            : ShelfText.Tooltip([(item.Paths[0], new FileFacts(false, item.SizeBytes))]);
        RemoveCommand = new RelayCommand(() => shelf.Remove(item.Id));
    }

    public ShelfItem Item { get; }

    public string Name => Item.DisplayName;

    public string RemoveName => "Remove " + Item.DisplayName;

    public string Tooltip { get; }

    public string BadgeText { get; }

    public bool HasBadge => BadgeText.Length > 0;

    public Brush BadgeBrush { get; }

    public Geometry Glyph { get; }

    public IRelayCommand RemoveCommand { get; }

    /// <summary>
    /// A file that is gone can't be dragged out: the tile says so for 2 s, then leaves (PRD 11). True when the drag may
    /// go ahead with what is still there.
    /// </summary>
    public bool CheckBeforeDrag()
    {
        if (Item.Paths.Any(p => _shelf.Facts(p) is not null))
        {
            _shelf.PruneItem(Item.Id);
            return true;
        }
        Gone = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _shelf.PruneItem(Item.Id);
        };
        timer.Start();
        return false;
    }
}
