using System.Windows.Controls;

namespace Gili.App.Island;

public partial class AlarmsPage : UserControl
{
    public AlarmsPage(AlarmsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
