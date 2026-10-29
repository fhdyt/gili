namespace Gili.Core.Layout;

/// <summary>
/// The mouse wheel over a page (plan 2f decision 10): a list that can still move in the wheel's direction scrolls,
/// and only otherwise does the wheel switch pages.
/// </summary>
public static class WheelScroll
{
    /// <summary>Whether a list at <paramref name="offset"/> of <paramref name="scrollable"/> moves for a wheel delta (positive is up).</summary>
    public static bool CanScroll(double offset, double scrollable, int delta) => delta switch
    {
        > 0 => offset >= 0.5,
        < 0 => offset <= scrollable - 0.5,
        _ => false,
    };
}
