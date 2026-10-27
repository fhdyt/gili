using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Gili.Platform.Shell;

namespace Gili.App.Settings;

/// <summary>The Settings window as in the mockup (.setw): its own title bar, sections on the left, rows on the right.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // Stays inside a small screen's work area (1366 × 768 at 150 %); the page scrolls instead.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 32);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
        Loaded += (_, _) => (Nav.ItemContainerGenerator.ContainerFromItem(Nav.SelectedItem) as UIElement)?.Focus();
        Closed += (_, _) => vm.Detach();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.RoundCorners(new WindowInteropHelper(this).Handle);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
