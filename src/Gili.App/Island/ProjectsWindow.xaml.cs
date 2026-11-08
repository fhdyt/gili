using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Gili.Platform.Interop;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// The Projects page in a window of its own (F-ISL-35), for seeing a project over many weeks. It has its own view model on
/// the same <see cref="ProjectService"/>, so an edit here or in the island shows in the other at once, while each keeps its
/// own form and list. Not topmost: it is a window to work in, like Tools.
/// </summary>
public partial class ProjectsWindow : Window
{
    private readonly ProjectsViewModel _vm;
    private readonly ProjectsPage _page;

    public ProjectsWindow(ProjectsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _page = new ProjectsPage(vm);
        PageHost.Content = _page;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 64);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 64);
        // Esc closes what is open on the page first, as in the island; the next one closes the window.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (!_page.Escape()) Close();
            e.Handled = true;
        };
        Loaded += (_, _) => Focus();
        // Nothing ticks here: a window left open over midnight moves its today when it is next used.
        Activated += (_, _) =>
        {
            if (vm.Today != ProjectService.Today) vm.Refresh();
        };
        StateChanged += (_, _) => UpdateMaximizeButton();
        Closed += (_, _) => vm.Detach();
    }

    /// <summary>Opening it again from the island brings it to the project the island has on show.</summary>
    public void ShowProject(long? id) => _vm.ShowProject(id);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.RoundCorners(hwnd);
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WindowMessages.WM_GETMINMAXINFO:
                // Left unhandled so WPF still applies MinWidth and MinHeight.
                WindowStyles.FitMaximizedToWorkArea(hwnd, lParam);
                break;
            case WindowMessages.WM_MOUSEHWHEEL:
                // Horizontal swipes and wheel tilts arrive only as this message; WPF has no event for them.
                _page.ScrollTimeline((short)((wParam.ToInt64() >> 16) & 0xFFFF));
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    private void UpdateMaximizeButton()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Glyph = (System.Windows.Media.Geometry)FindResource(maximized ? "Icon.Restore" : "Icon.Maximize");
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, maximized ? "Restore" : "Maximize");
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
