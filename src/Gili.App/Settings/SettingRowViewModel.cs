using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Modules;
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
    private readonly bool _changeable;
    private readonly string? _ownNote;
    private readonly HotkeyHooks? _hotkeys;
    private readonly FavoriteHooks? _favoriteHooks;
    private readonly CitySearcher? _searchCities;
    private DispatcherTimer? _cityDelay;
    private CancellationTokenSource? _citySearch;
    private bool _loading;

    /// <summary>The Weather city row: the chosen city, or a search field with up to five places under it (design 4c).</summary>
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private bool _isSearchingCity;
    [ObservableProperty] private string _cityQuery = "";
    [ObservableProperty] private string? _cityMessage;

    /// <summary>A Shortcuts row: the combination it holds, or "Press keys" while it records a new one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutText))]
    private string _shortcut = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutText))]
    private bool _isRecording;

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private string? _choice;
    [ObservableProperty] private double _value;
    [ObservableProperty] private double _min;
    [ObservableProperty] private double _max;
    [ObservableProperty] private string _valueText = "";

    /// <summary>Some rows lock and say why depending on others, like an island page whose left icon is off (plan 3a).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string? _note;

    [ObservableProperty] private bool _isEnabled;

    public SettingRowViewModel(SettingsHub hub, SettingRow row, string? note, bool changeable = true, HotkeyHooks? hotkeys = null,
        CitySearcher? searchCities = null, string? info = null, Action? run = null, string? button = null, FavoriteHooks? favorites = null)
    {
        _favoriteHooks = favorites;
        InfoText = info ?? "";
        ButtonText = button ?? (row.Kind == SettingKind.Action && row.Options is [var text, ..] ? text : null);
        RunCommand = new RelayCommand(() => run?.Invoke(), () => run is not null);
        _hotkeys = hotkeys;
        _searchCities = searchCities;
        _hub = hub;
        _row = row;
        _changeable = changeable;
        _ownNote = row.Available ? note ?? row.Help : SettingsCatalog.LaterVersion;
        Reload(hub.Current);
    }

    public string Label => _row.Label;

    /// <summary>The bar id of a system module, whose row can be dragged or moved with Alt + arrows; null otherwise.</summary>
    public string? ModuleId => SettingsCatalog.ModuleId(_row.Key);

    public bool CanReorder => ModuleId is not null;

    public string? ReorderHelp => CanReorder ? "Alt+Up or Alt+Down to reorder" : null;
    public bool HasNote => Note is not null;
    public bool IsSwitch => _row.Kind == SettingKind.Switch;
    public bool IsSegment => _row.Kind == SettingKind.Segment;
    public bool IsRange => _row.Kind == SettingKind.Range;
    public bool IsShortcut => _row.Kind == SettingKind.Shortcut;

    public string ShortcutText => IsRecording ? "Press keys" : Shortcut;

    public bool IsCity => _row.Kind == SettingKind.City;

    /// <summary>Favorite apps (F-LEFT-08): one line per app with Remove, and Add app… while fewer than eight.</summary>
    public bool IsFavorites => _row.Kind == SettingKind.Favorites;

    public ObservableCollection<FavoriteChoice> FavoriteRows { get; } = [];

    [ObservableProperty] private bool _canAddFavorite;

    /// <summary>Settings → Left side → Add app…: an .exe or a shortcut to one; anything else says so.</summary>
    public void AddFavoriteFile(string path)
    {
        if (_favoriteHooks?.KeyForFile(path) is not { } key)
        {
            Note = "Pick an app or a shortcut to one";
            return;
        }
        var app = new Gili.Core.Apps.FavoriteApp(key, _favoriteHooks.NameOf(key));
        _hub.Update(s => s.WithFavorites(Gili.Core.Apps.Favorites.Add(s.FavoriteList, app)));
    }

    private void ShowFavorites(AppSettings settings)
    {
        FavoriteRows.Clear();
        foreach (var app in settings.FavoriteList)
        {
            FavoriteRows.Add(new FavoriteChoice(app.Name, _favoriteHooks?.Icon(app),
                () => _hub.Update(s => s.WithFavorites(Gili.Core.Apps.Favorites.Remove(s.FavoriteList, app.Key)))));
        }
        CanAddFavorite = !Gili.Core.Apps.Favorites.IsFull(settings.FavoriteList);
    }

    /// <summary>Bar color (F-BAR-14): eight swatches and Custom, which opens a #RRGGBB field under the row.</summary>
    public bool IsColor => _row.Kind == SettingKind.Color;

    public ObservableCollection<SwatchChoice> Swatches { get; } = [];

    /// <summary>The colour in use is not a preset, so the Custom swatch is the selected one and shows it.</summary>
    [ObservableProperty] private bool _isCustomColor;
    [ObservableProperty] private System.Windows.Media.Brush? _customBrush;
    [ObservableProperty] private bool _isEditingColor;
    [ObservableProperty] private string _colorCode = "";

    public void EditCustomColor()
    {
        if (!IsEnabled) return;
        ColorCode = _hub.Current.BarColor;
        IsEditingColor = true;
    }

    /// <summary>Enter or leaving the field keeps a valid code; anything else says what a code looks like.</summary>
    public void CommitColorCode()
    {
        if (!IsEditingColor) return;
        if (BarColors.Parse(ColorCode) is not { } color)
        {
            Note = BarColors.BadCode;
            return;
        }
        IsEditingColor = false;
        _hub.Update(s => SettingsCatalog.Write(s, _row.Key, color));
    }

    public void CancelColorCode()
    {
        IsEditingColor = false;
        Reload(_hub.Current);
    }

    private void PickColor(string hex)
    {
        if (!IsEnabled) return;
        IsEditingColor = false;
        _hub.Update(s => SettingsCatalog.Write(s, _row.Key, hex));
    }

    private void ShowColor(string current)
    {
        if (Swatches.Count == 0)
        {
            foreach (var (name, hex) in BarColors.Presets) Swatches.Add(new SwatchChoice(name, hex, PickColor));
        }
        foreach (var swatch in Swatches) swatch.IsSelected = swatch.Hex == current;
        IsCustomColor = BarColors.NameOf(current) is null;
        CustomBrush = IsCustomColor ? SwatchChoice.BrushOf(current) : null;
    }

    /// <summary>An About row that shows text, like the version (design 5a).</summary>
    public bool IsInfo => _row.Kind == SettingKind.Info;

    public string InfoText { get; }

    public bool ShowInfo => InfoText.Length > 0;

    /// <summary>An About button, like Open logs folder, or Install update while an update waits (design 5d).</summary>
    public string? ButtonText { get; }

    public bool ShowButton => ButtonText is not null;

    public IRelayCommand RunCommand { get; }

    public ObservableCollection<CityChoice> CityResults { get; } = [];

    public void StartCitySearch()
    {
        if (!IsEnabled) return;
        CityQuery = "";
        CityMessage = null;
        CityResults.Clear();
        IsSearchingCity = true;
    }

    public void CancelCitySearch()
    {
        _cityDelay?.Stop();
        _citySearch?.Cancel();
        IsSearchingCity = false;
        CityResults.Clear();
        CityMessage = null;
    }

    /// <summary>Enter takes the first place found.</summary>
    public void PickFirstCity()
    {
        if (CityResults.Count > 0) PickCity(CityResults[0].Result);
    }

    private void PickCity(CityResult city)
    {
        CancelCitySearch();
        _hub.Update(s => s with { WeatherCity = city.Name, WeatherLatitude = city.Latitude, WeatherLongitude = city.Longitude });
    }

    /// <summary>Asks 400 ms after the last key, and only for two letters or more; a newer search wins.</summary>
    partial void OnCityQueryChanged(string value)
    {
        if (!IsSearchingCity) return;
        _cityDelay ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _cityDelay.Stop();
        _cityDelay.Tick -= OnCityDelay;
        _cityDelay.Tick += OnCityDelay;
        if (CitySearch.IsQuery(value)) _cityDelay.Start();
        else
        {
            CityResults.Clear();
            CityMessage = null;
        }
    }

    private void OnCityDelay(object? sender, EventArgs e)
    {
        _cityDelay!.Stop();
        _ = SearchCityAsync(CityQuery);
    }

    private async Task SearchCityAsync(string query)
    {
        if (_searchCities is null) return;
        _citySearch?.Cancel();
        var cancel = _citySearch = new CancellationTokenSource();
        try
        {
            var results = await _searchCities(query, cancel.Token);
            if (cancel.IsCancellationRequested || !IsSearchingCity) return;
            CityResults.Clear();
            foreach (var result in results) CityResults.Add(new CityChoice(result, PickCity));
            CityMessage = results.Count == 0 ? "No places found" : null;
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            if (!cancel.IsCancellationRequested) CityMessage = "Can't search right now. Check the connection";
        }
    }

    /// <summary>Starts recording: the hotkeys pause, so pressing the current combination reaches this row.</summary>
    public void StartRecording()
    {
        if (!IsEnabled || IsRecording) return;
        IsRecording = true;
        _hotkeys?.Suspend(true);
    }

    /// <summary>Ends recording; a combination is kept when it can be this action's, otherwise its row says why.</summary>
    public void StopRecording(Hotkey? recorded)
    {
        if (!IsRecording) return;
        IsRecording = false;
        _hotkeys?.Suspend(false);
        if (recorded is not { } hotkey || Hotkeys.ActionOf(_row.Key) is not { } action) return;
        if (Hotkeys.Problem(_hub.Current, action, hotkey) is { } problem)
        {
            Note = problem;
            return;
        }
        _hub.Update(s => SettingsCatalog.Write(s, _row.Key, Hotkeys.Format(hotkey)));
    }

    public IReadOnlyList<string> Options => _row.Options ?? [];

    /// <summary>Shows the stored value again, e.g. the island height clamped after the bar got lower.</summary>
    public void Reload(AppSettings settings)
    {
        _loading = true;
        // A row locked by another says why; otherwise it keeps its own note (help, or one about this machine).
        var state = _row.Available ? SettingsCatalog.State(settings, _row.Key) : new RowState(false, null);
        IsEnabled = _row.Available && _changeable && state.Enabled;
        Note = state.Note ?? _ownNote;
        var value = _row.Available && _row.Kind is not (SettingKind.Info or SettingKind.Action) ? SettingsCatalog.Read(settings, _row.Key) : _row.Default;
        switch (_row.Kind)
        {
            case SettingKind.Switch:
                IsOn = value is true;
                break;
            case SettingKind.Segment:
                Choice = value as string;
                break;
            case SettingKind.Shortcut:
                Shortcut = value as string ?? "";
                break;
            case SettingKind.City:
                City = value as string ?? "";
                break;
            case SettingKind.Color:
                ShowColor(value as string ?? BarColors.Default);
                break;
            case SettingKind.Favorites:
                ShowFavorites(settings);
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
        if (_loading || !IsEnabled) return;
        _hub.Update(s => SettingsCatalog.Write(s, _row.Key, value));
    }
}

/// <summary>One favorite app in Settings: its icon, its name and a Remove button.</summary>
public sealed class FavoriteChoice(string name, System.Windows.Media.ImageSource? icon, Action remove)
{
    public string Name { get; } = name;

    public System.Windows.Media.ImageSource? Icon { get; } = icon;

    public string RemoveName => "Remove " + Name;

    public IRelayCommand RemoveCommand { get; } = new RelayCommand(remove);
}

/// <summary>One colour of the Bar color row: a 20 px circle, ringed in white while it is the bar's.</summary>
public sealed partial class SwatchChoice(string name, string hex, Action<string> pick) : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string Name { get; } = name;

    public string Hex { get; } = hex;

    public System.Windows.Media.Brush Brush { get; } = BrushOf(hex);

    public IRelayCommand PickCommand { get; } = new RelayCommand(() => pick(hex));

    public static System.Windows.Media.Brush BrushOf(string hex)
    {
        var (r, g, b) = BarColors.Rgb(hex);
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>One place in the Weather city results.</summary>
public sealed class CityChoice(CityResult result, Action<CityResult> pick)
{
    public CityResult Result { get; } = result;

    public string Display => Result.Display;

    public IRelayCommand PickCommand { get; } = new RelayCommand(() => pick(result));
}
