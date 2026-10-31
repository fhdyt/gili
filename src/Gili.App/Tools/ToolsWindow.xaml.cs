using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Gili.Platform.Shell;

namespace Gili.App.Tools;

/// <summary>The Tools window (PRD 7.5): the Settings frame, resizable, tabs on the left.</summary>
public partial class ToolsWindow : Window
{
    private readonly ToolsViewModel _vm;

    public ToolsWindow(ToolsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        // Stays inside a small screen's work area; the tab scrolls instead.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 32);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
        Loaded += (_, _) => FocusTab();
        StateChanged += (_, _) => vm.SetMinimized(WindowState == WindowState.Minimized);
        vm.Alarms.PropertyChanged += OnAlarmsChanged;
        Closed += (_, _) =>
        {
            vm.Alarms.PropertyChanged -= OnAlarmsChanged;
            vm.Detach();
        };
    }

    /// <summary>Brings the window to the given tab, as Manage alarms does.</summary>
    public void ShowTab(string tab)
    {
        _vm.SelectedTab = tab;
        FocusTab();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.RoundCorners(new WindowInteropHelper(this).Handle);
    }

    private void FocusTab() => (Nav.ItemContainerGenerator.ContainerFromItem(Nav.SelectedItem) as UIElement)?.Focus();

    /// <summary>An opened editor puts the caret in its time, selected, so typing replaces it.</summary>
    private void OnAlarmsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AlarmsTabViewModel.Editor) || _vm.Alarms.Editor is null) return;
        Dispatcher.InvokeAsync(() =>
        {
            TimeBox.Focus();
            TimeBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
