namespace Gili.Core.Notes;

public readonly record struct NoteRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>Which side the open editor stays anchored to, so it grows toward the free side.</summary>
public enum GrowX { Left, Center, Right }

/// <summary>Down keeps the editor's top on the card's top; Up pins its content to the bottom edge.</summary>
public enum GrowY { Down, Up }

public sealed record NoteLayout(IReadOnlyList<NoteRect> Slots, double Height);

/// <summary>The masonry grid of note cards and the editor's frame (mockup layoutNotes, edTarget).</summary>
public static class NoteGrid
{
    public const double MinColumn = 140;
    public const double Gap = 8;
    public const double EditorWidth = 440;
    public const double EditorHeight = 360;
    private const double NewCardHeight = 64;

    public static int Columns(double width) => Math.Max(1, (int)Math.Floor((width + Gap) / (MinColumn + Gap)));

    public static double ColumnWidth(double width)
    {
        var columns = Columns(width);
        return (width - Gap * (columns - 1)) / columns;
    }

    /// <summary>Puts each card, in user order, in the shortest column; the first column wins a tie.</summary>
    public static NoteLayout Place(IReadOnlyList<double> heights, double width)
    {
        var columns = Columns(width);
        var columnWidth = ColumnWidth(width);
        var bottoms = new double[columns];
        var slots = new List<NoteRect>(heights.Count);
        foreach (var height in heights)
        {
            var column = 0;
            for (var i = 1; i < columns; i++)
                if (bottoms[i] < bottoms[column]) column = i;
            slots.Add(new NoteRect(column * (columnWidth + Gap), bottoms[column], columnWidth, height));
            bottoms[column] += height + Gap;
        }
        return new NoteLayout(slots, slots.Count == 0 ? 0 : bottoms.Max() - Gap);
    }

    /// <summary>The slot under the point, ignoring the card being dragged (<paramref name="except"/>); null when none.</summary>
    public static int? HitTest(IReadOnlyList<NoteRect> slots, double x, double y, int except)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (i == except) continue;
            var s = slots[i];
            if (x >= s.X && x < s.Right && y >= s.Y && y < s.Bottom) return i;
        }
        return null;
    }

    /// <summary>
    /// Where the editor opens for a card: 440 x 360 limited to the visible grid, anchored left, centred or right by which third of
    /// the grid the card's centre is in, downward when it fits and otherwise against the grid's bottom, then clamped into the grid.
    /// </summary>
    public static (NoteRect Frame, GrowX X, GrowY Y) Editor(NoteRect from, NoteRect visibleGrid)
    {
        var width = Math.Min(EditorWidth, visibleGrid.Width);
        var height = Math.Min(EditorHeight, visibleGrid.Height);
        var centre = from.X + from.Width / 2;
        var third = visibleGrid.Width / 3;

        GrowX growX;
        double x;
        if (centre < visibleGrid.X + third) { growX = GrowX.Left; x = from.X; }
        else if (centre > visibleGrid.X + 2 * third) { growX = GrowX.Right; x = from.Right - width; }
        else { growX = GrowX.Center; x = centre - width / 2; }

        var y = from.Y + height <= visibleGrid.Bottom ? from.Y : from.Bottom - height;

        x = Math.Clamp(x, visibleGrid.X, visibleGrid.Right - width);
        y = Math.Clamp(y, visibleGrid.Y, visibleGrid.Bottom - height);
        return (new NoteRect(x, y, width, height), growX, y < from.Y ? GrowY.Up : GrowY.Down);
    }

    /// <summary>The card a new note starts from: top right of the visible grid, one column wide, 64 high.</summary>
    public static NoteRect NewCard(NoteRect visibleGrid, double columnWidth) =>
        new(visibleGrid.Right - columnWidth, visibleGrid.Y, columnWidth, NewCardHeight);
}
