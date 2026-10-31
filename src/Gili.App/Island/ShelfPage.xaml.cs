using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gili.Core.Shelf;

namespace Gili.App.Island;

/// <summary>
/// The Shelf page as in the mockup (<c>.shsum</c>, <c>.shgrid</c>). A tile dragged 4 px leaves as the files it holds:
/// a Copy item offers only Copy and stays, a Cut item offers only Move, and files that moved away leave the Shelf
/// (PRD 6.3, F-ISL-30).
/// </summary>
public partial class ShelfPage : UserControl
{
    private const double DragStart = 4;

    private readonly IslandController _island;
    private Point? _pressed;

    public ShelfPage(ShelfViewModel vm, IslandController island)
    {
        _island = island;
        InitializeComponent();
        DataContext = vm;
    }

    private void OnTileDown(object sender, MouseButtonEventArgs e)
    {
        // The remove button handles its own click.
        if (e.OriginalSource is DependencyObject source && FindButton(source)) return;
        _pressed = e.GetPosition(this);
    }

    private void OnTileUp(object sender, MouseButtonEventArgs e) => _pressed = null;

    private void OnTileMove(object sender, MouseEventArgs e)
    {
        if (_pressed is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - start.X) < DragStart && Math.Abs(now.Y - start.Y) < DragStart) return;
        _pressed = null;
        if (sender is not FrameworkElement { DataContext: ShelfTileViewModel tile } element || tile.Item.Id < 0) return;
        if (!tile.CheckBeforeDrag()) return;
        var paths = tile.Item.Paths.Where(p => _island.Shelf.Facts(p) is not null).ToArray();
        _island.DraggingOut = true;
        try
        {
            DragDrop.DoDragDrop(element, new DataObject(DataFormats.FileDrop, paths),
                tile.Item.Mode == ShelfMode.Cut ? DragDropEffects.Move : DragDropEffects.Copy);
        }
        finally
        {
            _island.DraggingOut = false;
        }
        // Explorer's moves can report another effect, so what is still on disk decides (plan 3d).
        _island.Shelf.PruneItem(tile.Item.Id);
    }

    private static bool FindButton(DependencyObject element)
    {
        for (var e = element; e is not null and not ContentPresenter; e = System.Windows.Media.VisualTreeHelper.GetParent(e))
        {
            if (e is Button) return true;
        }
        return false;
    }
}
