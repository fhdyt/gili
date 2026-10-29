namespace Gili.Core.Layout;

/// <summary>
/// The mouse wheel over a page (PRD 6.4 "Pindah halaman", mockup #xp wheel): over a list that can scroll at all the
/// wheel only scrolls it, even at its ends, and only elsewhere does it switch pages.
/// </summary>
public static class WheelScroll
{
    /// <summary>The ScrollViewer's own step per wheel event: this many lines of 16 px, whatever the delta's size.</summary>
    private const double LineHeight = 16;

    /// <summary>Whether a list with <paramref name="scrollable"/> pixels to scroll keeps the wheel from switching pages.</summary>
    public static bool KeepsWheel(double scrollable) => scrollable >= 0.5;

    /// <summary>
    /// Where a list moves for one wheel event (a positive delta is up), as a ScrollViewer under the pointer would:
    /// Windows' wheel lines of 16 px, or the visible height when it is set to one screen at a time.
    /// </summary>
    public static double Offset(double offset, double scrollable, int delta, int lines, double viewport)
    {
        var step = lines > 0 ? lines * LineHeight : viewport;
        return Math.Clamp(offset - Math.Sign(delta) * step, 0, Math.Max(0, scrollable));
    }
}
