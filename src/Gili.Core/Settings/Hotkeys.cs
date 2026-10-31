using System.Globalization;

namespace Gili.Core.Settings;

/// <summary>Modifier keys, with RegisterHotKey's MOD_ values.</summary>
[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>What a global hotkey does (F-TOOL-10).</summary>
public enum HotkeyAction { Island, Clipboard, NewNote, AddTask }

/// <summary>A key combination: modifiers and a virtual-key code.</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, int Key)
{
    public override string ToString() => Hotkeys.Format(this);
}

/// <summary>
/// Gili's global hotkeys (F-TOOL-10, F-TOOL-11, PRD 6.5 Shortcuts): their defaults, their settings rows, and how a
/// combination reads and is written ("Ctrl+Alt+Space"). A hotkey needs Ctrl, Alt or Win, so plain typing never fires one.
/// </summary>
public static class Hotkeys
{
    public static readonly IReadOnlyList<(HotkeyAction Action, string Key, string Label, string Default)> All =
    [
        (HotkeyAction.Island, "hk.island", "Open or close the island", "Ctrl+Alt+Space"),
        (HotkeyAction.Clipboard, "hk.clipboard", "Clipboard", "Ctrl+Alt+V"),
        (HotkeyAction.NewNote, "hk.note", "New note", "Ctrl+Alt+N"),
        (HotkeyAction.AddTask, "hk.task", "Add task for today", "Ctrl+Alt+T"),
    ];

    public const string NeedsModifier = "Use Ctrl, Alt or Win with another key";

    private static readonly Dictionary<string, int> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Enter"] = 0x0D,
        ["Tab"] = 0x09,
        ["Backspace"] = 0x08,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        [";"] = 0xBA,
        ["="] = 0xBB,
        [","] = 0xBC,
        ["-"] = 0xBD,
        ["."] = 0xBE,
        ["/"] = 0xBF,
        ["`"] = 0xC0,
        ["["] = 0xDB,
        ["\\"] = 0xDC,
        ["]"] = 0xDD,
        ["'"] = 0xDE,
    };

    public static string Default(HotkeyAction action) => All.Single(h => h.Action == action).Default;

    public static string KeyOf(HotkeyAction action) => All.Single(h => h.Action == action).Key;

    public static HotkeyAction? ActionOf(string key) => All.Where(h => h.Key == key).Select(h => (HotkeyAction?)h.Action).FirstOrDefault();

    /// <summary>"Ctrl+Alt+Space" back into a hotkey; null when it isn't one (no Ctrl/Alt/Win, unknown key).</summary>
    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Ctrl,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" => HotkeyModifiers.Win,
                _ => (HotkeyModifiers)(-1),
            };
            if ((int)modifiers < 0) return null;
        }
        if (KeyCode(parts[^1]) is not { } key) return null;
        var hotkey = new Hotkey(modifiers, key);
        return IsValid(hotkey) ? hotkey : null;
    }

    public static string Format(Hotkey hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(hotkey.Key));
        return string.Join("+", parts);
    }

    /// <summary>Ctrl, Alt or Win, and a key that isn't itself a modifier.</summary>
    public static bool IsValid(Hotkey hotkey) =>
        (hotkey.Modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0 && !IsModifierKey(hotkey.Key) && hotkey.Key is > 0 and < 0xFF;

    /// <summary>Why a recorded combination can't be the action's hotkey, or null when it can.</summary>
    public static string? Problem(AppSettings settings, HotkeyAction action, Hotkey hotkey)
    {
        if (!IsValid(hotkey)) return NeedsModifier;
        foreach (var (other, _, label, _) in All)
        {
            if (other != action && Parse(settings.HotkeyFor(other)) == hotkey) return $"Already used for {label}";
        }
        return null;
    }

    public static bool IsModifierKey(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;

    private static int? KeyCode(string name)
    {
        if (Named.TryGetValue(name, out var named)) return named;
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0])) return char.ToUpperInvariant(name[0]);
        if (name.Length is 2 or 3 && (name[0] is 'F' or 'f') && int.TryParse(name[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 24)
            return 0x6F + f;
        return null;
    }

    private static string KeyName(int key)
    {
        if (key is >= 'A' and <= 'Z' or >= '0' and <= '9') return ((char)key).ToString();
        if (key is >= 0x70 and <= 0x87) return "F" + (key - 0x6F).ToString(CultureInfo.InvariantCulture);
        foreach (var (name, code) in Named)
        {
            if (code == key) return name;
        }
        return "0x" + key.ToString("X2", CultureInfo.InvariantCulture);
    }
}
