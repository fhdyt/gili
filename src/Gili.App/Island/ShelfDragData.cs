using System.Windows;
using Gili.Core.Shelf;
using Gili.Platform.Shelf;

namespace Gili.App.Island;

/// <summary>
/// What a file drag over the bar or the island carries (PRD 6.3 "Shelf"): files with paths, or files without one from
/// a zip folder, Outlook or a browser (F-ISL-31). Shared by both windows' drop handlers.
/// </summary>
public static class ShelfDragData
{
    public static bool Has(IDataObject data) => data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(VirtualFiles.DescriptorFormat);

    /// <summary>Hands the drop to the Shelf in its column; false when nothing was taken.</summary>
    public static bool Drop(IslandController island, IDataObject data, ShelfMode mode)
    {
        if (island.DraggingOut) return false;
        if (data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths) return island.DropFiles(paths, mode);
        return data is System.Runtime.InteropServices.ComTypes.IDataObject com && island.DropVirtualFiles(com, mode);
    }
}
