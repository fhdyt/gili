using System.Windows.Controls;

namespace Gili.App.Island;

/// <summary>The System page as in the mockup (<c>.sys</c>): five tiles, four with a one-minute chart.</summary>
public partial class SystemPage : UserControl
{
    public SystemPage(SystemViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // The three columns are equal, so one chart's width serves all four.
        FirstChart.SizeChanged += (_, e) => vm.SetChartWidth(e.NewSize.Width);
    }
}
