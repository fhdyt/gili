namespace Gili.Core.Apps;

/// <summary>A top-level window as the taskbar sees it; <paramref name="AppKey"/> groups windows of one app.</summary>
public sealed record TaskbarWindow(
    nint Handle, string AppKey, bool Visible, bool Tool, bool HasOwner, bool AppWindow, bool Cloaked, bool Own);

/// <summary>Which windows get a taskbar button (PRD 9.2): visible, not cloaked, not a tool window, unowned or app windows.</summary>
public static class TaskbarFilter
{
    public static bool Shows(TaskbarWindow window) =>
        window is { Visible: true, Cloaked: false, Own: false, Tool: false } && (!window.HasOwner || window.AppWindow);
}

/// <summary>How the strip fits: icons moved into "+N", and the room the active and hovered names may take.</summary>
public readonly record struct StripFit(int Hidden, double ActiveNameMax, double HoverNameMax, bool Tight);

/// <summary>
/// The running apps on the left of the bar (PRD 6.2, mockup <c>setApp()</c>, <c>fitApps()</c>): one icon per app in
/// the order apps were first opened, the active app named, and inactive icons dropping into "+N" when room runs out.
/// </summary>
public sealed class AppStrip
{
    private const double Icon = 28;
    private const double ActiveIcon = 36;
    private const double More = 38;
    private const double MinLeft = 60;
    private const double MaxName = 160;
    private const double NameCapsule = 12;
    private const double MinHoverName = 40;

    private readonly List<string> _apps = [];

    public IReadOnlyList<string> Apps => _apps;

    public string? Active { get; private set; }

    /// <summary>Takes the current windows; new apps join at the end and apps without windows leave. True when the list changed.</summary>
    public bool Update(IEnumerable<TaskbarWindow> windows)
    {
        var present = new List<string>();
        foreach (var window in windows)
        {
            if (TaskbarFilter.Shows(window) && !present.Contains(window.AppKey)) present.Add(window.AppKey);
        }
        var changed = _apps.RemoveAll(app => !present.Contains(app)) > 0;
        foreach (var app in present)
        {
            if (_apps.Contains(app)) continue;
            _apps.Add(app);
            changed = true;
        }
        if (Active is not null && !_apps.Contains(Active)) Active = null;
        return changed;
    }

    /// <summary>The foreground app becomes active only when it is on the strip (the desktop or Gili itself are not).</summary>
    public bool SetActive(string? appKey)
    {
        if (appKey is null || !_apps.Contains(appKey)) return false;
        Active = appKey;
        return true;
    }

    /// <summary>
    /// Inactive icons leave from the right until 60 px are left over; the active app always stays. The active name gets
    /// what is left (at most 160), and a hovered name only what the active name leaves, or it falls back to a tooltip.
    /// </summary>
    public static StripFit Fit(double available, int count, int activeIndex, double activeNameWidth)
    {
        var hidden = 0;
        double Need() => (count - hidden - 1) * Icon + ActiveIcon + (hidden > 0 ? More : 0);
        for (var k = count - 1; k >= 0 && available - Need() < MinLeft; k--)
        {
            if (k == activeIndex) continue;
            hidden++;
        }
        var free = Math.Max(0, available - Need());
        var hover = Math.Min(MaxName, free - Math.Min(MaxName, Math.Min(free, activeNameWidth)) - NameCapsule);
        return new(hidden, Math.Min(MaxName, free), Math.Max(0, hover), hover < MinHoverName);
    }
}

/// <summary>
/// The left and right sides stop 24 px short of the island (PRD 6.1 "Aturan jarak", mockup <c>fitBar()</c>): measured from
/// the hovered compact island plus what sits beside it, and never closer to the middle than 150 px.
/// </summary>
public static class BarSpacing
{
    private const double Gap = 24;
    private const double Edge = 10;
    private const double MinHalf = 150;

    public static double LeftLimit(double screenWidth, double compactWidth, double islandHeight, bool giliCircle, int privacyDots) =>
        Limit(screenWidth, compactWidth, privacyDots * 13 + (giliCircle ? islandHeight + 6 : 0));

    public static double RightLimit(double screenWidth, double compactWidth, double islandHeight, int bubbles) =>
        Limit(screenWidth, compactWidth, bubbles * (islandHeight + 6));

    private static double Limit(double screenWidth, double compactWidth, double beside) =>
        screenWidth / 2 - Math.Max(compactWidth / 2 + 8 + beside, MinHalf) - Gap - Edge;
}
