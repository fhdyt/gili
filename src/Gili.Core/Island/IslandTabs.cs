using Gili.Core.Settings;

namespace Gili.Core.Island;

/// <summary>
/// Which tabs the open island's menu shows (PRD 6.3 "Tab bisa disembunyikan", F-ISL-23): hidden ones leave the menu
/// and the arrow/swipe order, but a hidden page opened directly shows its tab at its own place for as long as it is open.
/// </summary>
public static class IslandTabs
{
    public static IReadOnlyList<IslandPage> Menu { get; } = [.. Enum.GetValues<IslandPage>().Where(p => p != IslandPage.Controls)];

    public static PageSet Of(IslandPage page) => page == IslandPage.Controls ? PageSet.None : (PageSet)(1 << (int)page);

    public static IReadOnlyList<IslandPage> Visible(PageSet hidden, IslandPage? temporary) =>
        [.. Menu.Where(p => p == temporary || !hidden.HasFlag(Of(p)))];

    /// <summary>The arrow keys' and swipes' next tab, stopping at either end.</summary>
    public static IslandPage Step(IReadOnlyList<IslandPage> visible, IslandPage current, int direction)
    {
        var index = visible.ToList().IndexOf(current);
        if (index < 0) return current;
        return visible[Math.Clamp(index + direction, 0, visible.Count - 1)];
    }

    /// <summary>Where the island goes when the page it shows is hidden: the nearest visible tab on the left, else the right.</summary>
    public static IslandPage Fallback(PageSet hidden, IslandPage page)
    {
        var index = Menu.ToList().IndexOf(page);
        for (var i = index - 1; i >= 0; i--)
            if (!hidden.HasFlag(Of(Menu[i]))) return Menu[i];
        for (var i = index + 1; i < Menu.Count; i++)
            if (!hidden.HasFlag(Of(Menu[i]))) return Menu[i];
        return IslandPage.Media;
    }
}
