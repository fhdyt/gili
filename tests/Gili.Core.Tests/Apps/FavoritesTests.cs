using Gili.Core.Apps;
using Gili.Core.Settings;

namespace Gili.Core.Tests.Apps;

public class FavoritesTests
{
    private static readonly FavoriteApp WhatsApp = new("aumid:5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", "WhatsApp");
    private static readonly FavoriteApp Code = new(@"exe:C:\Program Files\Microsoft VS Code\Code.exe", "Visual Studio Code");
    private static readonly FavoriteApp Chrome = new(@"exe:C:\Program Files\Google\Chrome\Application\chrome.exe", "Google Chrome");

    [Fact]
    public void Adding_puts_the_app_at_the_end_once()
    {
        var list = Favorites.Add([WhatsApp], Code);
        Assert.Equal([WhatsApp, Code], list);
        Assert.Same(list, Favorites.Add(list, Code with { Name = "Code" }));
    }

    [Fact]
    public void Keys_compare_without_case() =>
        Assert.True(Favorites.Contains([Code], Code.Key.ToUpperInvariant()));

    [Fact]
    public void At_most_eight()
    {
        IReadOnlyList<FavoriteApp> list = [.. Enumerable.Range(0, Favorites.Max).Select(i => new FavoriteApp($"exe:C:\\{i}.exe", $"App {i}"))];
        Assert.True(Favorites.IsFull(list));
        Assert.Same(list, Favorites.Add(list, Code));
    }

    [Fact]
    public void Removing_and_moving()
    {
        IReadOnlyList<FavoriteApp> list = [WhatsApp, Code, Chrome];
        Assert.Equal([WhatsApp, Chrome], Favorites.Remove(list, Code.Key));
        Assert.Equal([Code, WhatsApp, Chrome], Favorites.Move(list, Code.Key, -1));
        Assert.Equal([WhatsApp, Chrome, Code], Favorites.Move(list, Code.Key, +1));
        // Already at an end: nothing changes, and the same list comes back.
        Assert.Same(list, Favorites.Move(list, WhatsApp.Key, -1));
        Assert.Same(list, Favorites.Remove(list, "exe:C:\\none.exe"));
    }

    [Fact]
    public void Only_store_and_desktop_apps_can_be_favorites()
    {
        Assert.True(Favorites.CanAdd(WhatsApp.Key));
        Assert.True(Favorites.CanAdd(Code.Key));
        Assert.False(Favorites.CanAdd("hwnd:1234"));
        Assert.False(Favorites.CanAdd("exe:"));
    }

    [Fact]
    public void Settings_start_empty_and_repair_a_broken_list()
    {
        Assert.Empty(new AppSettings().FavoriteList);
        var broken = new AppSettings().WithFavorites([Code, Code with { Name = "again" }, new FavoriteApp("", "nothing"),
            .. Enumerable.Range(0, 9).Select(i => new FavoriteApp($"exe:C:/{i}.exe", $"{i}"))]);
        var fixedList = broken.Normalize().FavoriteList;
        Assert.Equal(Favorites.Max, fixedList.Count);
        Assert.Equal(Code, fixedList[0]);
        Assert.Equal(fixedList.Count, fixedList.Select(f => f.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void The_list_round_trips_as_text_and_settings_compare_by_value()
    {
        var s = new AppSettings().WithFavorites([WhatsApp, Code]);
        Assert.Equal([WhatsApp, Code], s.FavoriteList);
        Assert.Equal(s, s.Normalize());
        Assert.Equal(s, new AppSettings().WithFavorites([WhatsApp, Code]));
        Assert.Equal([WhatsApp, Code], (IReadOnlyList<FavoriteApp>)SettingsCatalog.Read(s, "favs"));
        Assert.Equal([Code], SettingsCatalog.Write(s, "favs", new List<FavoriteApp> { Code }).FavoriteList);
    }

    [Fact]
    public void Names_cannot_break_the_text() =>
        Assert.Equal("A B", Favorites.Parse(Favorites.Format([new FavoriteApp("exe:C:/a.exe", "A" + (char)9 + "B")]))[0].Name);

    [Fact]
    public void Favorites_switch_hides_them_without_a_tab()
    {
        Assert.False(new AppSettings().HiddenLeft.HasFlag(LeftIcons.Favorites));
        Assert.Equal(PageSet.None, HiddenRules.PageOf(LeftIcons.Favorites));
        Assert.Equal(false, SettingsCatalog.Read(new AppSettings { HiddenLeft = LeftIcons.Favorites }, "L.Favorites"));
        // The list can't change while the favorites are off.
        Assert.False(SettingsCatalog.State(new AppSettings { HiddenLeft = LeftIcons.Favorites }, "favs").Enabled);
        Assert.True(SettingsCatalog.State(new AppSettings(), "favs").Enabled);
    }
}
