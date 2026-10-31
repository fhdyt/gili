using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Bar;
using Gili.App.Controls;
using Gili.Core.Modules;
using Gili.Platform.Modules;

namespace Gili.App.Island;

/// <summary>
/// The Volume island (PRD 6.4): the speaker button mutes, the slider sets the level at once and follows the volume keys
/// through the existing Core Audio callback, and the output row opens the speakers to pick the default from.
/// </summary>
public partial class VolumeIsland : UserControl, IControlIsland
{
    private static readonly Brush Filled = Frozen(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Brush Empty = Frozen(Color.FromArgb(0x29, 0xFF, 0xFF, 0xFF));

    private readonly RightService _right;
    private bool _syncing;
    private bool _open;

    public VolumeIsland(RightService right)
    {
        _right = right;
        InitializeComponent();
        right.Changed += () =>
        {
            if (_open) Refresh();
        };
    }

    public event Action? Resized;

    public void Shown()
    {
        _open = true;
        DeviceList.Visibility = Visibility.Collapsed;
        ChevronTurn.Angle = 0;
        Refresh();
        ReadDevices();
    }

    public void Hidden() => _open = false;

    private void Refresh()
    {
        var volume = _right.Volume;
        var missing = volume is null;
        Unavailable.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        Slider.Visibility = DeviceButton.Visibility = missing ? Visibility.Collapsed : Visibility.Visible;
        MuteButton.IsEnabled = !missing;
        if (volume is not { } v)
        {
            Level.Text = "";
            Speaker.Glyph = Glyph(VolumeLevel.Mute);
            return;
        }
        Level.Text = VolumeText.Tooltip(v.Percent, v.Muted).Replace("Volume ", "", StringComparison.Ordinal);
        Speaker.Glyph = Glyph(VolumeText.Level(v.Percent, v.Muted));
        AutomationProperties.SetName(MuteButton, v.Muted ? "Unmute" : "Mute");
        Slider.Foreground = v.Muted ? Empty : Filled;
        // A drag in progress owns the slider; the callback's echo would make the thumb stutter.
        if (Slider.IsMouseCaptureWithin) return;
        _syncing = true;
        Slider.Value = v.Percent;
        _syncing = false;
    }

    private void ReadDevices()
    {
        var outputs = AudioDevices.Outputs();
        DeviceName.Text = outputs.FirstOrDefault(o => o.IsDefault)?.Name ?? "";
        Devices.ItemsSource = outputs
            .Select(o => new ControlRowItem(o.Name, "", Glyph(o.Headphones), o.IsDefault, o.Id, Check: o.IsDefault))
            .ToList();
    }

    private Geometry Glyph(VolumeLevel level) => (Geometry)FindResource("Icon.Volume." + level);

    private Geometry Glyph(bool headphones) => (Geometry)FindResource(headphones ? "Icon.Headphones" : "Icon.Volume.Mid");

    private void OnMute(object sender, RoutedEventArgs e)
    {
        if (_right.Volume is { } v) _right.SetMute(!v.Muted);
    }

    private void OnSlide(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_syncing) _right.SetVolume((int)Math.Round(e.NewValue));
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        Slider.Value = ControlSliders.Wheel((int)Slider.Value, e.Delta);
        e.Handled = true;
    }

    private void OnDevices(object sender, RoutedEventArgs e)
    {
        var open = DeviceList.Visibility != Visibility.Visible;
        if (open) ReadDevices();
        DeviceList.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ChevronTurn.BeginAnimation(RotateTransform.AngleProperty, BezierEase.To(open ? 90 : 0, 200, BezierEase.Out));
        Resized?.Invoke();
    }

    private void OnDevice(object sender, RoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not ControlRowItem { Tag: string id }) return;
        AudioDevices.SetDefault(id);
        DeviceList.Visibility = Visibility.Collapsed;
        ChevronTurn.BeginAnimation(RotateTransform.AngleProperty, BezierEase.To(0, 200, BezierEase.Out));
        ReadDevices();
        Resized?.Invoke();
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
