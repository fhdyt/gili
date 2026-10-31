namespace Gili.Core.Notes;

/// <summary>The edges a floating note is being resized from; a corner is two of them.</summary>
[Flags]
public enum ResizeEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
}

/// <summary>
/// Where a floating note goes and how it sizes (PRD 6.3 "Note mengambang", mockup defaultFloat, clampF and the .fr
/// handles): 260 × 220 at first, 200 × 140 to 520 × 640, always inside the work area. Rectangles are in DIPs.
/// </summary>
public static class NoteFloat
{
    public const double Width = 260;
    public const double Height = 220;
    public const double MinWidth = 200;
    public const double MinHeight = 140;
    public const double MaxWidth = 520;
    public const double MaxHeight = 640;
    public const int Max = 6;
    public const double Cascade = 24;
    public const double EdgeBand = 6;
    public const double CornerBand = 12;
    public const double Corner = 12;

    public static bool CanFloat(int floating) => floating < Max;

    /// <summary>Float on top: 24 px from the work area's right edge and top, each further note 24 px down and to the left.</summary>
    public static NoteRect Default(int floating, NoteRect work)
    {
        var step = Cascade * (floating + 1);
        return Clamp(new NoteRect(work.Right - Width - step, work.Y + step, Width, Height), work);
    }

    /// <summary>The size within its limits and the work area, then the position moved inside it.</summary>
    public static NoteRect Clamp(NoteRect rect, NoteRect work)
    {
        var w = Math.Min(Math.Clamp(rect.Width, MinWidth, MaxWidth), work.Width);
        var h = Math.Min(Math.Clamp(rect.Height, MinHeight, MaxHeight), work.Height);
        return new NoteRect(
            Math.Clamp(rect.X, work.X, work.Right - w),
            Math.Clamp(rect.Y, work.Y, work.Bottom - h),
            w,
            h);
    }

    /// <summary>
    /// The window a dragged note grows into when let go outside the island: 260 × 220 with the point it was held by
    /// (<paramref name="rx"/>, <paramref name="ry"/> as fractions of its size) still under the pointer.
    /// </summary>
    public static NoteRect Released(double px, double py, double rx, double ry) =>
        new(px - rx * Width, py - ry * Height, Width, Height);

    /// <summary>Which edges a point inside the note grabs: 6 px along each edge, 12 × 12 at the corners.</summary>
    public static ResizeEdges HitEdge(double x, double y, double width, double height)
    {
        var left = x < CornerBand;
        var right = x >= width - CornerBand;
        var top = y < CornerBand;
        var bottom = y >= height - CornerBand;
        // A corner square takes both edges.
        if ((left || right) && (top || bottom))
            return (left ? ResizeEdges.Left : ResizeEdges.Right) | (top ? ResizeEdges.Top : ResizeEdges.Bottom);
        var edges = ResizeEdges.None;
        if (x < EdgeBand) edges |= ResizeEdges.Left;
        else if (x >= width - EdgeBand) edges |= ResizeEdges.Right;
        if (y < EdgeBand) edges |= ResizeEdges.Top;
        else if (y >= height - EdgeBand) edges |= ResizeEdges.Bottom;
        return edges;
    }

    /// <summary>
    /// The note after the pointer moved by (<paramref name="dx"/>, <paramref name="dy"/>) from where it grabbed these
    /// edges; the opposite edges stay put, within the size limits and the work area (mockup .fr pointermove).
    /// </summary>
    public static NoteRect Resize(NoteRect start, ResizeEdges edges, double dx, double dy, NoteRect work)
    {
        var (x, y, w, h) = (start.X, start.Y, start.Width, start.Height);
        if (edges.HasFlag(ResizeEdges.Right)) w = Math.Min(Math.Min(MaxWidth, work.Right - x), Math.Max(MinWidth, start.Width + dx));
        if (edges.HasFlag(ResizeEdges.Bottom)) h = Math.Min(Math.Min(MaxHeight, work.Bottom - y), Math.Max(MinHeight, start.Height + dy));
        if (edges.HasFlag(ResizeEdges.Left))
        {
            w = Math.Min(Math.Min(MaxWidth, start.Right - work.X), Math.Max(MinWidth, start.Width - dx));
            x = start.Right - w;
        }
        if (edges.HasFlag(ResizeEdges.Top))
        {
            h = Math.Min(Math.Min(MaxHeight, start.Bottom - work.Y), Math.Max(MinHeight, start.Height - dy));
            y = start.Bottom - h;
        }
        return new NoteRect(x, y, w, h);
    }

    /// <summary>The corner radius while the dragged note grows into the window: from the card's 12 or the editor's 16 to 12.</summary>
    public static double Radius(double from, double progress) => from + (Corner - from) * Math.Clamp(progress, 0, 1);
}
