using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gili.Core.Apps;
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

    public AppsService(Func<double> dpiScale)
    {
        _dpiScale = dpiScale;
        _coalesce.Tick += (_, _) =>
        {
            _coalesce.Stop();
            Read();
        };
        _windows.Changed += () =>
        {
            if (!_coalesce.IsEnabled) _coalesce.Start();
        };
        _windows.ForegroundChanged += OnForeground;
    }

    /// <summary>First read, once the bar shows, so icons come at the bar's DPI.</summary>
    public void Start()
    {
        Read();
        OnForeground(Platform.Shell.ForegroundWindow.Get());
    }

    public IReadOnlyList<AppItem> Apps => [.. _strip.Apps.Select(key => _items[key])];

    public string? Active => _strip.Active;

    /// <summary>The list or the active app changed.</summary>
    public event Action? Changed;

    /// <summary>Brings the app's topmost window to the front, like its taskbar button.</summary>
    public void Activate(string key)
    {
        var window = _last.FirstOrDefault(w => w.AppKey == key);
        if (window is null) return;
        TaskbarWindows.BringToFront(window.Handle);
        if (_strip.SetActive(key)) Changed?.Invoke();
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
    }

    private void OnForeground(nint hwnd)
    {
        // A window that just opened may not be on the strip yet; read first so it can become active straight away.
        var key = _windows.AppKeyOf(hwnd);
        if (!_strip.Apps.Contains(key)) Read();
        if (_strip.SetActive(key)) Changed?.Invoke();
    }

    private AppItem Resolve(TaskbarWindow window)
    {
        var size = (int)Math.Round(16 * _dpiScale());
        var (name, pixels) = AppInfo.Resolve(window.AppKey, window.Handle, size);
        ImageSource? icon = null;
        if (pixels is not null)
        {
            var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32, null, pixels.Bgra, pixels.Width * 4);
            bitmap.Freeze();
            icon = bitmap;
        }
        return new AppItem(window.AppKey, name, icon);
    }
}
