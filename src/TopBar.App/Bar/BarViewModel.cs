using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TopBar.Core.Settings;

namespace TopBar.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    [ObservableProperty] private Brush _tint = Brushes.Transparent;

    public void ApplySettings(AppSettings settings, BarAppearance look)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(look.TintOpacity * 255), 0x16, 0x16, 0x18));
        brush.Freeze();
        Tint = brush;
    }
}
