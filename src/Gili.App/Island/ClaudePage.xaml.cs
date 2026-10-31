using System.Windows.Controls;

namespace Gili.App.Island;

public partial class ClaudePage : UserControl
{
    public ClaudePage(ClaudePageViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
