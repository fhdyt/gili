namespace Gili.Core.Apps;

/// <summary>
/// A favorite app on the left of the bar (F-LEFT-08, revision 14, issue #36). <paramref name="Key"/> is the strip's app
/// key: <c>aumid:&lt;AppUserModelID&gt;</c> for Store and pinned apps, <c>exe:&lt;path&gt;</c> for desktop apps.
/// </summary>
public sealed record FavoriteApp(string Key, string Name);

/// <summary>The favorites list: in the user's order, each app once, at most <see cref="Max"/>. Unchanged lists come back as the same instance.</summary>
public static class Favorites
{
    public const int Max = 8;

    public static bool CanAdd(string key) =>
        (key.StartsWith("aumid:", StringComparison.Ordinal) && key.Length > 6) || (key.StartsWith("exe:", StringComparison.Ordinal) && key.Length > 4);

    public static bool Contains(IReadOnlyList<FavoriteApp> list, string key) =>
        list.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsFull(IReadOnlyList<FavoriteApp> list) => list.Count >= Max;

    public static IReadOnlyList<FavoriteApp> Add(IReadOnlyList<FavoriteApp> list, FavoriteApp app) =>
        !CanAdd(app.Key) || IsFull(list) || Contains(list, app.Key) ? list : [.. list, app];

    public static IReadOnlyList<FavoriteApp> Remove(IReadOnlyList<FavoriteApp> list, string key) =>
        Contains(list, key) ? [.. list.Where(f => !string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))] : list;

    /// <summary>One step left (−1) or right (+1).</summary>
    public static IReadOnlyList<FavoriteApp> Move(IReadOnlyList<FavoriteApp> list, string key, int delta)
    {
        var from = IndexOf(list, key);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= list.Count) return list;
        var moved = list.ToList();
        (moved[from], moved[to]) = (moved[to], moved[from]);
        return moved;
    }

    /// <summary>The stored list as the bar can use it: valid keys only, each once, at most eight.</summary>
    public static IReadOnlyList<FavoriteApp> Repair(IReadOnlyList<FavoriteApp>? list)
    {
        if (list is null) return [];
        var valid = list.Where(f => f is not null && CanAdd(f.Key ?? "")).DistinctBy(f => f.Key, StringComparer.OrdinalIgnoreCase).Take(Max).ToList();
        return valid.Count == list.Count ? list : valid;
    }

    /// <summary>One app per line, its key and name split by a tab; neither can hold a line break or a tab.</summary>
    public static string Format(IReadOnlyList<FavoriteApp> list) =>
        string.Join(Line, list.Select(f => Clean(f.Key) + Tab + Clean(f.Name)));

    public static IReadOnlyList<FavoriteApp> Parse(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : [.. text.Split(Line).Select(line => line.Split(Tab, 2)).Select(p => new FavoriteApp(p[0], p.Length > 1 ? p[1] : p[0]))];

    private const char Line = (char)10;
    private const char Tab = (char)9;

    private static string Clean(string text) => new([.. text.Select(c => char.IsControl(c) ? ' ' : c)]);

    private static int IndexOf(IReadOnlyList<FavoriteApp> list, string key)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Key, key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
