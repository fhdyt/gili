using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core;
using Gili.Core.Settings;

namespace Gili.App.Settings;

/// <summary>The Settings window (PRD 6.5): sections on the left, the chosen section's rows on the right. Every change applies at once.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsHub _hub;
    private readonly bool _transparencyEnabled;
    private readonly InstallKind _kind;
    private readonly StartupState? _startup;
    private readonly Func<string?>? _claudeStatus;

    [ObservableProperty] private SettingsSection _selectedSection;
    [ObservableProperty] private IReadOnlyList<SettingRowViewModel> _rows = [];

    public SettingsViewModel(SettingsHub hub, bool transparencyEnabled, InstallKind kind, StartupState? startup, Func<string?>? claudeStatus = null)
    {
        _claudeStatus = claudeStatus;
        _hub = hub;
        _transparencyEnabled = transparencyEnabled;
        _kind = kind;
        _startup = startup;
        _selectedSection = Sections[0];
        BuildRows();
        hub.Changed += Reload;
    }

    public IReadOnlyList<SettingsSection> Sections => SettingsCatalog.Sections;

    /// <summary>Stops following the settings once the window has closed.</summary>
    public void Detach() => _hub.Changed -= Reload;

    partial void OnSelectedSectionChanged(SettingsSection value) => BuildRows();

    private void BuildRows()
    {
        _order = _hub.Current.ModuleOrder;
        Rows = [.. SettingsCatalog.RowsOf(SelectedSection, _hub.Current).Select(r => new SettingRowViewModel(_hub, r, NoteFor(r.Key), CanChange(r.Key)))];
    }

    private string? _order;

    /// <summary>Moves a system module one row up (−1) or down (+1), and the bar with it (PRD 6.4).</summary>
    public void Move(SettingRowViewModel row, int delta)
    {
        if (row.ModuleId is { } id) _hub.Update(s => s with { ModuleOrder = string.Join(",", RightModuleRules.Move(s.Order, id, delta)) });
    }

    /// <summary>Puts a dragged system module at <paramref name="index"/> among the system modules.</summary>
    public void MoveTo(SettingRowViewModel row, int index)
    {
        if (row.ModuleId is { } id) _hub.Update(s => s with { ModuleOrder = string.Join(",", RightModuleRules.MoveTo(s.Order, id, index)) });
    }

    /// <summary>The Store copy cannot turn its startup task back on once Task Manager switched it off (PRD 9.4).</summary>
    private bool CanChange(string key) =>
        key != "auto" || _kind != InstallKind.Store || _startup is not { } state || StartupRules.CanChange(state);

    /// <summary>Notes that depend on the machine instead of the row (plan 1f decision 5).</summary>
    private string? NoteFor(string key) => key switch
    {
        "auto" when _kind == InstallKind.Development => "Only the installed copy starts with Windows",
        "auto" when _kind == InstallKind.Store && _startup is { } state => StartupRules.Note(state),
        // The integration's status line (PRD 9.4): waiting, connected, or what went wrong.
        "clOn" => _claudeStatus?.Invoke(),
        "glass" when !_transparencyEnabled => "Transparency effects are off in Windows, so the bar is drawn solid.",
        _ => null,
    };

    /// <summary>A status line changed outside the settings, e.g. the first Claude hook arrived.</summary>
    public void RefreshNotes()
    {
        if (SelectedSection.Name == "Claude") BuildRows();
    }

    private void Reload(AppSettings settings)
    {
        // A new order lists the rows again; the window puts the focus back on the row that moved.
        if (settings.ModuleOrder != _order && SelectedSection.Name == "Right side")
        {
            BuildRows();
            return;
        }
        foreach (var row in Rows) row.Reload(settings);
    }
}
