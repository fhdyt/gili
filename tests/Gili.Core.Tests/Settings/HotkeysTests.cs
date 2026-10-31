using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

/// <summary>F-TOOL-10, F-TOOL-11: global hotkeys and their settings (design 4b).</summary>
public sealed class HotkeysTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x20)]
    [InlineData("ctrl+alt+v", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'V')]
    [InlineData("Win+Shift+F5", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x74)]
    [InlineData("Alt+7", HotkeyModifiers.Alt, '7')]
    [InlineData("Ctrl+.", HotkeyModifiers.Ctrl, 0xBE)]
    public void Parses_and_formats_back(string text, HotkeyModifiers modifiers, int key)
    {
        var hotkey = Hotkeys.Parse(text);
        Assert.Equal(new Hotkey(modifiers, key), hotkey);
        Assert.Equal(hotkey, Hotkeys.Parse(Hotkeys.Format(hotkey!.Value)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("V")]
    [InlineData("Shift+V")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Hyper+V")]
    [InlineData("Ctrl+F25")]
    public void Rejects_what_typing_or_nothing_would_fire(string? text) => Assert.Null(Hotkeys.Parse(text));

    [Fact]
    public void Defaults_follow_the_prd()
    {
        var s = new AppSettings();
        Assert.True(s.KeyboardShortcuts);
        Assert.Equal(["Ctrl+Alt+Space", "Ctrl+Alt+V", "Ctrl+Alt+N", "Ctrl+Alt+T"],
            Hotkeys.All.Select(h => (string)SettingsCatalog.Read(s, h.Key)));
    }

    [Fact]
    public void A_new_hotkey_is_written_unless_another_action_has_it()
    {
        var s = new AppSettings();
        var changed = SettingsCatalog.Write(s, "hk.note", "Win+Shift+n");
        Assert.Equal("Shift+Win+N", changed.HotkeyNewNote);
        Assert.Equal("Already used for Clipboard", Hotkeys.Problem(s, HotkeyAction.NewNote, Hotkeys.Parse("Ctrl+Alt+V")!.Value));
        Assert.Equal(s, SettingsCatalog.Write(s, "hk.note", "Ctrl+Alt+V"));
        Assert.Equal(Hotkeys.NeedsModifier, Hotkeys.Problem(s, HotkeyAction.NewNote, new Hotkey(HotkeyModifiers.Shift, 'N')));
    }

    [Fact]
    public void Shortcut_rows_lock_while_shortcuts_are_off()
    {
        var off = new AppSettings { KeyboardShortcuts = false };
        Assert.False(SettingsCatalog.State(off, "hk.island").Enabled);
        Assert.True(SettingsCatalog.State(new AppSettings(), "hk.island").Enabled);
    }
}
