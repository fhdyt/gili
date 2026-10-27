using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimerViewModel.Fraction)) DrawRing();
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
