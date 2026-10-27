using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Settings;

namespace Gili.App.Settings;

/// <summary>
/// One row of the Settings window. Available rows read and write <see cref="AppSettings"/> through
/// <see cref="SettingsCatalog"/>; the others show their PRD default and say they come later (PRD 6.5).
/// </summary>
public sealed partial class SettingRowViewModel : ObservableObject
{
    private readonly SettingsHub _hub;
    private readonly SettingRow _row;
    private bool _loading;

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private string? _choice;
    [ObservableProperty] private double _value;
    [ObservableProperty] private double _min;
    [ObservableProperty] private double _max;
    [ObservableProperty] private string _valueText = "";

    public SettingRowViewModel(SettingsHub hub, SettingRow row, string? note)
    {
        _hub = hub;
        _row = row;
        Note = row.Available ? note ?? row.Help : SettingsCatalog.LaterVersion;
        Reload(hub.Current);
    }

    public string Label => _row.Label;
    public string? Note { get; }
    public bool HasNote => Note is not null;
    public bool IsEnabled => _row.Available;
    public bool IsSwitch => _row.Kind == SettingKind.Switch;
    public bool IsSegment => _row.Kind == SettingKind.Segment;
    public bool IsRange => _row.Kind == SettingKind.Range;
    public IReadOnlyList<string> Options => _row.Options ?? [];

    /// <summary>Shows the stored value again, e.g. the island height clamped after the bar got lower.</summary>
    public void Reload(AppSettings settings)
    {
        _loading = true;
        var value = _row.Available ? SettingsCatalog.Read(settings, _row.Key) : _row.Default;
        switch (_row.Kind)
        {
            case SettingKind.Switch:
                IsOn = value is true;
                break;
            case SettingKind.Segment:
                Choice = value as string;
                break;
            case SettingKind.Range:
                (Min, Max) = SettingsCatalog.Range(settings, _row.Key);
                Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                ValueText = ((int)Value).ToString(CultureInfo.InvariantCulture) + _row.Unit;
                break;
        }
        _loading = false;
    }

    partial void OnIsOnChanged(bool value) => Push(value);

    partial void OnChoiceChanged(string? value)
    {
        if (value is not null) Push(value);
    }

    partial void OnValueChanged(double value)
    {
        ValueText = ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture) + _row.Unit;
        Push(value);
    }

    private void Push(object value)
    {
        if (_loading || !_row.Available) return;
        _hub.Update(s => SettingsCatalog.Write(s, _row.Key, value));
    }
}
