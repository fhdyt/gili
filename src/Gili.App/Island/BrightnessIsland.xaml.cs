using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gili.App.Bar;
using Gili.Core.Modules;

namespace Gili.App.Island;

/// <summary>The Brightness island (PRD 6.4): the level is read when it opens, and the slider sets it at once.</summary>
public partial class BrightnessIsland : UserControl, IControlIsland
{
    private readonly RightService _right;
    private bool _syncing;

    public BrightnessIsland(RightService right)
    {
        _right = right;
        InitializeComponent();
    }

    public event Action? Resized;

    public void Shown()
    {
        var level = _right.ReadBrightness();
        Unavailable.Visibility = level is null ? Visibility.Visible : Visibility.Collapsed;
        Slider.Visibility = level is null ? Visibility.Collapsed : Visibility.Visible;
        _syncing = true;
        Slider.Value = level ?? 0;
        _syncing = false;
        Show(level);
        Resized?.Invoke();
    }

    public void Hidden()
    {
    }

    private void Show(int? level) => Level.Text = level is { } l ? l.ToString(CultureInfo.InvariantCulture) + "%" : "";

    private void OnSlide(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing) return;
        var level = (int)Math.Round(e.NewValue);
        _right.SetBrightness(level);
        Show(level);
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        Slider.Value = ControlSliders.Wheel((int)Slider.Value, e.Delta);
        e.Handled = true;
    }
}
