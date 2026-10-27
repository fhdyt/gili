using CommunityToolkit.Mvvm.ComponentModel;

namespace Gili.App.Tools;

/// <summary>The Tools window (PRD 7.5): tabs on the left; Alarms works, the others arrive in later plans.</summary>
public sealed partial class ToolsViewModel(AlarmsTabViewModel alarms, string tab) : ObservableObject
{
    public const string AlarmsTab = "Alarms";

    public IReadOnlyList<string> Tabs { get; } = ["Timer", "Stopwatch", "Pomodoro", AlarmsTab, "Todo"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlarms))]
    private string _selectedTab = tab;

    public AlarmsTabViewModel Alarms { get; } = alarms;

    public bool IsAlarms => SelectedTab == AlarmsTab;

    public void Detach() => Alarms.Detach();
}
