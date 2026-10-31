using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Controls;
using Gili.Core.Timers;

namespace Gili.App.Island;

public partial class TimerPage : UserControl
{
    private const double Radius = 48;
    private readonly TimerViewModel _vm;

    public TimerPage(TimerViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnChanged;
        DrawRing();
        foreach (var column in Columns)
        {
            column.ValueChanged += () => vm.Pick(PickedSeconds);
            column.Settled += () =>
            {
                if (Columns.All(c => !c.IsMoving)) vm.CommitPick();
            };
        }
        ShowWheel(animate: false);
        TurnWheel(animate: false);
    }

    private WheelColumnView[] Columns => [HoursWheel, MinutesWheel, SecondsWheel];

    private int PickedSeconds => TimerWheel.Combine(HoursWheel.Value, MinutesWheel.Value, SecondsWheel.Value);

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TimerViewModel.Fraction):
                DrawRing();
                break;
            case nameof(TimerViewModel.ShowWheel):
                ShowWheel(animate: IsVisible);
                break;
            case nameof(TimerViewModel.WheelSeconds):
                TurnWheel(animate: IsVisible);
                break;
        }
    }

    /// <summary>A preset, Reset or a finished timer sets the length from outside: the picker turns to it (F-ISL-29).</summary>
    private void TurnWheel(bool animate)
    {
        if (_vm.WheelSeconds == PickedSeconds) return;
        var (h, m, s) = TimerWheel.Split(_vm.WheelSeconds);
        HoursWheel.SetTo(h, animate);
        MinutesWheel.SetTo(m, animate);
        SecondsWheel.SetTo(s, animate);
    }

    /// <summary>The picker and the digits cross-fade over 150 ms in the same 120 px.</summary>
    private void ShowWheel(bool animate)
    {
        var wheel = _vm.ShowWheel;
        Wheel.IsHitTestVisible = wheel;
        Digits.IsHitTestVisible = !wheel;
        var duration = TimeSpan.FromMilliseconds(animate && SystemParameters.ClientAreaAnimation ? 150 : 0);
        Wheel.BeginAnimation(OpacityProperty, new DoubleAnimation(wheel ? 1 : 0, duration));
        Digits.BeginAnimation(OpacityProperty, new DoubleAnimation(wheel ? 0 : 1, duration));
        if (wheel) _vm.Pick(PickedSeconds);
    }

    /// <summary>The time left as an arc from the top, clockwise (mockup .bring, rotated −90°), on the ring's 48 px centre line.</summary>
    private void DrawRing()
    {
        var fraction = Math.Clamp(_vm.Fraction, 0, 1);
        var centre = new Point(52, 52);
        if (fraction >= 0.9999)
        {
            Ring.Data = new EllipseGeometry(centre, Radius - 4, Radius - 4);
            return;
        }
        if (fraction <= 0)
        {
            Ring.Data = Geometry.Empty;
            return;
        }
        var r = Radius - 4;
        var angle = fraction * 2 * Math.PI;
        var start = new Point(centre.X, centre.Y - r);
        var end = new Point(centre.X + r * Math.Sin(angle), centre.Y - r * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        Ring.Data = geometry;
    }
}
