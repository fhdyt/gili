using System.Runtime.InteropServices;
using Gili.Core.Settings;

namespace Gili.Platform.Input;

/// <summary>
/// Gili's global hotkeys (F-TOOL-10, PRD 9.2 "Hotkey": RegisterHotKey). They go to a message-only window made on the
/// thread that creates this service, so WM_HOTKEY arrives through the UI thread's own message loop. A combination another
/// app already holds stays off and is reported, for Settings' "Used by another app" (PRD 11).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly nint MessageOnly = -3;

    private readonly WndProc _proc;
    private readonly nint _window;
    private readonly HashSet<HotkeyAction> _failed = [];
    private IReadOnlyDictionary<HotkeyAction, Hotkey> _wanted = new Dictionary<HotkeyAction, Hotkey>();
    private bool _suspended;

    public HotkeyService()
    {
        _proc = OnMessage;
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "GiliHotkeys" + Environment.ProcessId,
        };
        RegisterClassEx(ref wc);
        _window = CreateWindowEx(0, wc.lpszClassName, "", 0, 0, 0, 0, 0, MessageOnly, 0, wc.hInstance, 0);
    }

    /// <summary>A hotkey was pressed, on the UI thread.</summary>
    public event Action<HotkeyAction>? Pressed;

    /// <summary>The set that failed to register changed.</summary>
    public event Action? FailuresChanged;

    /// <summary>Actions whose combination another app holds.</summary>
    public IReadOnlySet<HotkeyAction> Failed => _failed;

    /// <summary>Registers exactly these hotkeys; an empty set turns them all off.</summary>
    public void Apply(IReadOnlyDictionary<HotkeyAction, Hotkey> hotkeys)
    {
        _wanted = hotkeys;
        Register();
    }

    /// <summary>
    /// While Settings records a new combination, the old ones must not fire (their keys would never reach the window).
    /// </summary>
    public void Suspend(bool suspended)
    {
        if (_suspended == suspended) return;
        _suspended = suspended;
        Register();
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_window != 0) DestroyWindow(_window);
    }

    private void Register()
    {
        UnregisterAll();
        var before = _failed.ToHashSet();
        _failed.Clear();
        if (!_suspended && _window != 0)
        {
            foreach (var (action, hotkey) in _wanted)
            {
                if (!RegisterHotKey(_window, (int)action + 1, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.Key)) _failed.Add(action);
            }
        }
        // While suspended nothing is registered, which is no failure.
        if (_suspended) _failed.UnionWith(before);
        if (!_failed.SetEquals(before)) FailuresChanged?.Invoke();
    }

    private void UnregisterAll()
    {
        if (_window == 0) return;
        foreach (var action in Enum.GetValues<HotkeyAction>()) UnregisterHotKey(_window, (int)action + 1);
    }

    private nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmHotkey && wParam.ToInt32() - 1 is var id && Enum.IsDefined((HotkeyAction)id))
        {
            Pressed?.Invoke((HotkeyAction)id);
            return 0;
        }
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? name);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
