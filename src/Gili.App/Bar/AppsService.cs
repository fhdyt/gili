using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gili.Core.Apps;
using Gili.Core.Claude;
using Gili.Platform.Apps;

namespace Gili.App.Bar;

/// <summary>One app on the strip: its name and own icon, cached per app.</summary>
public sealed record AppItem(string Key, string Name, ImageSource? Icon);

/// <summary>
/// The running apps for the left of the bar (F-LEFT-01): reads the taskbar's windows when one appears or goes (events
/// coalesced over 100 ms), follows the foreground app at once, and brings an app's top window to the front.
/// </summary>
public sealed class AppsService : IDisposable
{
    private readonly TaskbarWindows _windows = new();
    private readonly AppStrip _strip = new();
    private readonly Dictionary<string, AppItem> _items = [];
    private readonly DispatcherTimer _coalesce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Func<double> _dpiScale;
    private IReadOnlyList<TaskbarWindow> _last = [];
    private bool _showStrip = true;

    public AppsService(Func<double> dpiScale)
    {
        _dpiScale = dpiScale;
        _coalesce.Tick += (_, _) =>
        {
            _coalesce.Stop();
            if (_showStrip) Read();
            else WatchClaude(_windows.Read());
        };
        _windows.Changed += () =>
        {
            if (!_coalesce.IsEnabled) _coalesce.Start();
        };
        _windows.ForegroundChanged += OnForeground;
        _windows.WindowShown += (hwnd, className) => WindowShown?.Invoke(hwnd, className);
    }

    /// <summary>Any top-level window appeared, with its class name (Explorer's progress dialog for F-ISL-15).</summary>
    public event Action<nint, string>? WindowShown;

    /// <summary>A window of Claude Desktop has a taskbar button: Claude's sessions and 5-hour timer show (revision 17).</summary>
    public bool ClaudeDesktopOpen { get; private set; }

    public event Action<bool>? ClaudeDesktopChanged;

    /// <summary>First read, once the bar shows, so icons come at the bar's DPI.</summary>
    public void Start()
    {
        if (!_showStrip)
        {
            WatchClaude(_windows.Read());
            return;
        }
        Read();
        OnForeground(Platform.Shell.ForegroundWindow.Get());
    }

    /// <summary>
    /// Settings → Left side → Running apps (F-LEFT-07). While off the window list is read only to see whether Claude
    /// Desktop is open, with no icons; the foreground window is still followed, since the Clipboard page's paste and
    /// Claude's sessions need it.
    /// </summary>
    public void ShowStrip(bool show)
    {
        if (_showStrip == show) return;
        _showStrip = show;
        if (!show)
        {
            _coalesce.Stop();
            return;
        }
        Read();
        OnForeground(Platform.Shell.ForegroundWindow.Get());
    }

    public IReadOnlyList<AppItem> Apps => [.. _strip.Apps.Select(key => _items[key])];

    public string? Active => _strip.Active;

    /// <summary>The list or the active app changed.</summary>
    public event Action? Changed;

    /// <summary>A window came to the foreground (a Claude session's finish may be seen now, F-CLD-04).</summary>
    public event Action<nint>? ForegroundChanged;

    /// <summary>Brings the app's topmost window to the front, like its taskbar button.</summary>
    public void Activate(string key)
    {
        var window = _last.FirstOrDefault(w => w.AppKey == key);
        if (window is null) return;
        TaskbarWindows.BringToFront(window.Handle);
        if (_strip.SetActive(key)) Changed?.Invoke();
    }

    /// <summary>A favorite's name and icon (F-LEFT-08), resolved once per app and shared with the strip.</summary>
    public AppItem Item(FavoriteApp favorite)
    {
        if (_items.TryGetValue(favorite.Key, out var known)) return known;
        var size = (int)Math.Round(16 * _dpiScale());
        var (name, pixels) = AppInfo.Resolve(favorite.Key, 0, size);
        var item = new AppItem(favorite.Key, name == "App" ? favorite.Name : name, Image(pixels));
        _items[favorite.Key] = item;
        return item;
    }

    /// <summary>
    /// A window belongs to a favorite by its app key, or, for a favorite added from an exe in Settings, by its process's
    /// exe: Chrome, Edge or VS Code windows are keyed by their AppUserModelID, their shortcut's target by its path.
    /// </summary>
    private static bool Same(TaskbarWindow window, string favoriteKey) =>
        string.Equals(window.AppKey, favoriteKey, StringComparison.OrdinalIgnoreCase)
        || (favoriteKey.StartsWith("exe:", StringComparison.Ordinal)
            && string.Equals(TaskbarWindows.ProcessPath(window.Handle), favoriteKey[4..], StringComparison.OrdinalIgnoreCase));

    /// <summary>The favorite a running app already is, matched like <see cref="Same"/>; null when it is none.</summary>
    public FavoriteApp? FavoriteOf(string appKey, IReadOnlyList<FavoriteApp> favorites)
    {
        var window = _last.FirstOrDefault(w => w.AppKey == appKey);
        return favorites.FirstOrDefault(f => string.Equals(f.Key, appKey, StringComparison.OrdinalIgnoreCase) || (window is not null && Same(window, f.Key)));
    }

    /// <summary>The name the strip shows for a running app, for Add to Favorites.</summary>
    public string NameOf(string key) => _items.TryGetValue(key, out var item) ? item.Name : "App";

    /// <summary>
    /// A favorite's click: its window to the front when it runs (read fresh, since the strip may be switched off), else
    /// the app starts. False when it could not start.
    /// </summary>
    public bool Open(FavoriteApp favorite)
    {
        var window = _windows.Read().FirstOrDefault(w => Same(w, favorite.Key));
        if (window is null) return AppLauncher.Launch(favorite.Key);
        TaskbarWindows.BringToFront(window.Handle);
        if (_showStrip && _strip.SetActive(window.AppKey)) Changed?.Invoke();
        return true;
    }

    public void Dispose()
    {
        _coalesce.Stop();
        _windows.Dispose();
    }

    private void Read()
    {
        _last = _windows.Read();
        foreach (var window in _last)
        {
            if (!_items.ContainsKey(window.AppKey)) _items[window.AppKey] = Resolve(window);
        }
        if (_strip.Update(_last)) Changed?.Invoke();
        WatchClaude(_last);
    }

    private void WatchClaude(IReadOnlyList<TaskbarWindow> windows)
    {
        var open = ClaudeDesktopApp.IsOpen(windows);
        if (open == ClaudeDesktopOpen) return;
        ClaudeDesktopOpen = open;
        ClaudeDesktopChanged?.Invoke(open);
    }

    private void OnForeground(nint hwnd)
    {
        ForegroundChanged?.Invoke(hwnd);
        if (!_showStrip) return;
        // A window that just opened may not be on the strip yet; read first so it can become active straight away.
        var key = _windows.AppKeyOf(hwnd);
        if (!_strip.Apps.Contains(key)) Read();
        if (_strip.SetActive(key)) Changed?.Invoke();
    }

    private AppItem Resolve(TaskbarWindow window)
    {
        var size = (int)Math.Round(16 * _dpiScale());
        var (name, pixels) = AppInfo.Resolve(window.AppKey, window.Handle, size);
        return new AppItem(window.AppKey, name, Image(pixels));
    }

    private static ImageSource? Image(IconPixels? pixels)
    {
        if (pixels is null) return null;
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32, null, pixels.Bgra, pixels.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
