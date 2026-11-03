namespace Gili.Core.Settings;

/// <summary>
/// The left side's icons that Settings → Left side can hide (PRD 6.2), stored as names in settings.json; Apps is the
/// strip of running apps after the divider (F-LEFT-07), and Favorites the favorite apps before it (F-LEFT-08). Clipboard
/// lost its icon in revision 21 but keeps its name, so an older settings.json still reads; Normalize drops it.
/// </summary>
[Flags]
public enum LeftIcons { None = 0, Start = 1, Timer = 2, Alarms = 4, Todo = 8, Notes = 16, Clipboard = 32, Shelf = 64, Claude = 128, Apps = 256, Favorites = 512, Projects = 1024 }

/// <summary>
/// The island's pages, as a set: the ones Settings → Island hides (PRD 6.3 "Tab bisa disembunyikan"). Stored as names, so
/// Projects (revision 16) took the next free bit although its tab sits after Todo. Clipboard is the page revision 21
/// removed, kept by name like <see cref="LeftIcons.Clipboard"/> and left out of All.
/// </summary>
[Flags]
public enum PageSet
{
    None = 0,
    Media = 1,
    Timer = 2,
    Alarms = 4,
    Calendar = 8,
    Todo = 16,
    Notes = 32,
    Clipboard = 64,
    Shelf = 128,
    Claude = 256,
    System = 512,
    Projects = 1024,
    All = 2047 & ~Clipboard,
}

public static class HiddenRules
{
    /// <summary>The page a left icon opens, which its switch hides too; Start has none.</summary>
    public static PageSet PageOf(LeftIcons icon) => icon switch
    {
        LeftIcons.Timer => PageSet.Timer,
        LeftIcons.Alarms => PageSet.Alarms,
        LeftIcons.Todo => PageSet.Todo,
        LeftIcons.Projects => PageSet.Projects,
        LeftIcons.Notes => PageSet.Notes,
        LeftIcons.Shelf => PageSet.Shelf,
        LeftIcons.Claude => PageSet.Claude,
        _ => PageSet.None,
    };

    /// <summary>The pages of every hidden left icon.</summary>
    public static PageSet PagesOf(LeftIcons hidden) =>
        Enum.GetValues<LeftIcons>().Where(i => i != LeftIcons.None && hidden.HasFlag(i)).Aggregate(PageSet.None, (set, i) => set | PageOf(i));
}
