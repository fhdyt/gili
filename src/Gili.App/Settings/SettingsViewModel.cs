using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core;
using Gili.Core.About;
using Gili.Core.Settings;

namespace Gili.App.Settings;

/// <summary>What the Shortcuts rows need from the hotkeys: pause them while recording, and which ones another app holds.</summary>
public sealed record HotkeyHooks(Action<bool> Suspend, Func<HotkeyAction, bool> Failed);

/// <summary>The Weather city row's search (design 4c).</summary>
public delegate Task<IReadOnlyList<Gili.Core.Modules.CityResult>> CitySearcher(string name, CancellationToken cancel);

/// <summary>What Settings → About shows and does (design 5a).</summary>
/// <param name="AnotherCopy">The Version row's note when the other release path is installed too (F-PUB-07); found off the UI thread.</param>
/// <param name="Updates">Where the update check stands (design 5d).</param>
/// <param name="ReleaseNotes">The release page to open, once one is known.</param>
public sealed record AboutActions(string VersionLine, Action OpenLogs, Action PrepareUninstall, Task<string?>? AnotherCopy = null,
    Func<UpdateState>? Updates = null, Action? InstallUpdate = null, Func<string?>? ReleaseNotes = null, Action<string>? Open = null);

/// <summary>The Settings window (PRD 6.5): sections on the left, the chosen section's rows on the right. Every change applies at once.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsHub _hub;
    private readonly bool _transparencyEnabled;
    private readonly InstallKind _kind;
    private readonly StartupState? _startup;
    private readonly Func<string?>? _claudeStatus;
    private readonly HotkeyHooks? _hotkeys;
    private readonly CitySearcher? _searchCities;
    private readonly AboutActions? _about;

    [ObservableProperty] private SettingsSection _selectedSection;
    [ObservableProperty] private IReadOnlyList<SettingRowViewModel> _rows = [];

    public SettingsViewModel(SettingsHub hub, bool transparencyEnabled, InstallKind kind, StartupState? startup, Func<string?>? claudeStatus = null,
        HotkeyHooks? hotkeys = null, CitySearcher? searchCities = null, AboutActions? about = null)
    {
        _about = about;
        _hotkeys = hotkeys;
        _searchCities = searchCities;
        _claudeStatus = claudeStatus;
        _hub = hub;
        _transparencyEnabled = transparencyEnabled;
        _kind = kind;
        _startup = startup;
        _selectedSection = Sections[0];
        BuildRows();
        hub.Changed += Reload;
        // Last: a search that already finished continues at once and lists the rows again.
        if (about?.AnotherCopy is { } anotherCopy) _ = ShowAnotherCopyAsync(anotherCopy);
    }

    public IReadOnlyList<SettingsSection> Sections => SettingsCatalog.Sections;

    /// <summary>Stops following the settings once the window has closed.</summary>
    public void Detach() => _hub.Changed -= Reload;

    partial void OnSelectedSectionChanged(SettingsSection value) => BuildRows();

    private void BuildRows()
    {
        _order = _hub.Current.ModuleOrder;
        Rows = [.. SettingsCatalog.RowsOf(SelectedSection, _hub.Current, _kind).Select(r =>
            new SettingRowViewModel(_hub, r, NoteFor(r.Key), CanChange(r.Key), _hotkeys, _searchCities, InfoFor(r.Key), RunFor(r.Key),
                r.Key == "updates" && Update is { } update ? UpdateRules.Button(update) : null))];
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

    private UpdateState? Update => _about?.Updates?.Invoke();

    private string? ReleaseNotes => _about?.ReleaseNotes?.Invoke();

    private string? InfoFor(string key) => key switch
    {
        "version" => _about?.VersionLine,
        "updates" when Update is { } update => UpdateRules.Row(update).Text,
        _ => null,
    };

    /// <summary>The update check changed: About shows it at once.</summary>
    public void RefreshUpdates()
    {
        if (SelectedSection.Name == "About") BuildRows();
    }

    private string? _anotherCopy;

    private async Task ShowAnotherCopyAsync(Task<string?> found)
    {
        _anotherCopy = await found;
        if (_anotherCopy is not null && SelectedSection.Name == "About") BuildRows();
    }

    private Action? RunFor(string key) => key switch
    {
        "logs" => _about?.OpenLogs,
        "updates" when Update?.Status == UpdateStatus.Available => _about?.InstallUpdate,
        "notes" when ReleaseNotes is { } page && _about?.Open is { } open => () => open(page),
        "prepare" => _about?.PrepareUninstall,
        _ => null,
    };

    /// <summary>The Store copy cannot turn its startup task back on once Task Manager switched it off (PRD 9.4).</summary>
    private bool CanChange(string key) => key switch
    {
        "auto" => _kind != InstallKind.Store || _startup is not { } state || StartupRules.CanChange(state),
        "updates" => Update is { Status: not UpdateStatus.Off },
        "notes" => ReleaseNotes is not null,
        _ => true,
    };

    /// <summary>Notes that depend on the machine instead of the row (plan 1f decision 5).</summary>
    private string? NoteFor(string key) => key switch
    {
        "auto" when _kind == InstallKind.Development => "Only the installed copy starts with Windows",
        "auto" when _kind == InstallKind.Store && _startup is { } state => StartupRules.Note(state),
        // The integration's status line (PRD 9.4): waiting, connected, or what went wrong.
        "clOn" => _claudeStatus?.Invoke(),
        "version" => _anotherCopy,
        "updates" when Update is { } update => UpdateRules.Row(update).Note,
        "notes" when ReleaseNotes is null => SettingsCatalog.LaterVersion,
        "glass" when !_transparencyEnabled => "Transparency effects are off in Windows, so the bar is drawn solid.",
        // PRD 11: a combination another app holds stays off.
        _ when Hotkeys.ActionOf(key) is { } action && _hotkeys?.Failed(action) == true => "Used by another app",
        _ => null,
    };

    /// <summary>A hotkey registered or failed after a change: its row says so.</summary>
    public void RefreshShortcutNotes()
    {
        if (SelectedSection.Name == "Shortcuts") BuildRows();
    }

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
