using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Claude;
using Gili.Core.Island;

namespace Gili.App.Island;

/// <summary>
/// The Claude page (PRD 6.3 page 9, F-CLD-05, mockup renderClPage): one row per live session, waiting first. A row's
/// click brings its window forward and counts its finish as seen. Running times tick each second only while the page
/// shows a working session (PRD 8.1).
/// </summary>
public sealed partial class ClaudePageViewModel : ObservableObject
{
    private readonly IslandController _island;
    private readonly Func<bool> _use24Hour;
    private readonly DispatcherTimer _second = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _shown;

    [ObservableProperty] private bool _isEmpty = true;

    /// <summary>
    /// The 5-hour window's row above the sessions (mockup limRow): ring, Resets 19:00, the time left; No window running while
    /// the timer shows none. A click lets the reset time shown in Claude Desktop be typed (revision 18).
    /// </summary>
    [ObservableProperty] private bool _hasWindow;
    [ObservableProperty] private bool _windowLimitHit;
    [ObservableProperty] private string _windowResets = "";
    [ObservableProperty] private string _windowLeft = "";
    [ObservableProperty] private string _windowTooltip = "";

    /// <summary>The ring's arc for the time left, in stroke widths (the ring is 2.75 px thick on a 51.8 px circle).</summary>
    [ObservableProperty] private System.Windows.Media.DoubleCollection _windowArc = [0, 100];

    /// <summary>The row is a form: Resets at, the time, and the check.</summary>
    [ObservableProperty] private bool _isEditingReset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetState))]
    private string _resetText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetState))]
    private bool _isResetInvalid;

    /// <summary>The time field's red edge (the Field style's Tag) for a time no running window could reset at.</summary>
    public string? ResetState => IsResetInvalid ? "Invalid" : null;

    public const string ResetHint = "Click to set the reset time shown in Claude";

    /// <summary>
    /// The weekly limit's row under the 5-hour one (revision 19): ring of the week, Resets Sun 8 Nov, 04:00, and the time
    /// left; Not set until its day and time are typed from Claude Desktop.
    /// </summary>
    [ObservableProperty] private string _weeklyResets = "";
    [ObservableProperty] private string _weeklyLeft = "";
    [ObservableProperty] private string _weeklyTooltip = "";
    [ObservableProperty] private System.Windows.Media.DoubleCollection _weeklyArc = [0, 100];

    /// <summary>The weekly row is a form: Resets on, ‹ day ›, the time, and the check.</summary>
    [ObservableProperty] private bool _isEditingWeekly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeeklyDay))]
    [NotifyCanExecuteChangedFor(nameof(PreviousDayCommand), nameof(NextDayCommand))]
    private int _weeklyDayIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeeklyState))]
    private string _weeklyTimeText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeeklyState))]
    private bool _isWeeklyInvalid;

    private IReadOnlyList<DateOnly> _weeklyDays = [];

    /// <summary>The stepper's day: Sun 8 Nov.</summary>
    public string WeeklyDay => _weeklyDays.Count == 0 ? "" : ClaudeWeeklyText.Day(_weeklyDays[WeeklyDayIndex]);

    public string? WeeklyState => IsWeeklyInvalid ? "Invalid" : null;

    public const string WeeklyHint = "Click to set the weekly reset shown in Claude";

    public ClaudePageViewModel(IslandController island, Func<bool> use24Hour)
    {
        _island = island;
        _use24Hour = use24Hour;
        _second.Tick += (_, _) => Refresh();
        island.ClaudeChanged += Refresh;
        island.ClaudeWindowTick += RefreshWindow;
        island.ViewChanged += view =>
        {
            _shown = view.Mode == IslandMode.Expanded && view.Page == IslandPage.Claude;
            Refresh();
        };
        Refresh();
    }

    public ObservableCollection<ClaudeRowViewModel> Rows { get; } = [];

    /// <summary>Rows came or went, so the open island fits the list again.</summary>
    public event Action? ContentChanged;

    private void RefreshWindow()
    {
        var hadWindow = HasWindow;
        HasWindow = _island.ClaudeTimerShown;
        if (!HasWindow)
        {
            IsEditingReset = false;
            IsEditingWeekly = false;
        }
        RefreshWeekly();
        if (_island.ClaudeWindow is { } window)
        {
            var view = ClaudeUsageText.For(window, DateTimeOffset.Now, _use24Hour());
            WindowLimitHit = view.LimitHit;
            WindowResets = view.Resets;
            WindowLeft = view.Left;
            WindowTooltip = view.Tooltip + Environment.NewLine + ResetHint;
            const double Circle = 18.85;
            WindowArc = [Math.Max(0.001, Circle * (1 - view.Elapsed)), 100];
        }
        else
        {
            WindowLimitHit = false;
            WindowResets = "No window running";
            WindowLeft = "";
            WindowTooltip = ResetHint;
            WindowArc = [0.001, 100];
        }
        if (hadWindow != HasWindow) ContentChanged?.Invoke();
    }

    private void RefreshWeekly()
    {
        if (_island.ClaudeWeekly is { } reset)
        {
            var view = ClaudeWeeklyText.For(reset, DateTimeOffset.Now, _use24Hour());
            WeeklyResets = view.Resets;
            WeeklyLeft = view.Left;
            WeeklyTooltip = view.Tooltip + Environment.NewLine + WeeklyHint;
            const double Circle = 18.85;
            WeeklyArc = [Math.Max(0.001, Circle * (1 - view.Elapsed)), 100];
        }
        else
        {
            WeeklyResets = "Not set";
            WeeklyLeft = "";
            WeeklyTooltip = WeeklyHint;
            WeeklyArc = [0.001, 100];
        }
    }

    /// <summary>The weekly row's click: the form opens on the day and time set, else today with no time.</summary>
    [RelayCommand]
    private void EditWeekly()
    {
        IsEditingReset = false;
        var now = DateTimeOffset.Now;
        _weeklyDays = ClaudeWeekly.Days(now);
        var set = _island.ClaudeWeekly?.ResetsAt.ToLocalTime();
        var index = set is { } at ? Array.IndexOf([.. _weeklyDays], DateOnly.FromDateTime(at.DateTime)) : 0;
        WeeklyDayIndex = Math.Max(0, index);
        // The index is often 0 again, which raises nothing: the stepper asked before there were days and stayed off.
        OnPropertyChanged(nameof(WeeklyDay));
        PreviousDayCommand.NotifyCanExecuteChanged();
        NextDayCommand.NotifyCanExecuteChanged();
        WeeklyTimeText = set is { } time
            ? time.ToString(_use24Hour() ? "HH:mm" : "h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
            : "";
        IsWeeklyInvalid = false;
        IsEditingWeekly = true;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousDay() => WeeklyDayIndex--;

    [RelayCommand(CanExecute = nameof(CanGoOn))]
    private void NextDay() => WeeklyDayIndex++;

    private bool CanGoBack() => WeeklyDayIndex > 0;

    private bool CanGoOn() => WeeklyDayIndex < _weeklyDays.Count - 1;

    [RelayCommand]
    private void CancelWeekly() => IsEditingWeekly = false;

    /// <summary>Enter or the check: a day and time within the next week becomes the weekly reset; any other stays, in red.</summary>
    [RelayCommand]
    public void SaveWeekly()
    {
        if (_weeklyDays.Count == 0 || ClaudeWeekly.ParseReset(_weeklyDays[WeeklyDayIndex], WeeklyTimeText, DateTimeOffset.Now) is not { } reset)
        {
            IsWeeklyInvalid = true;
            return;
        }
        IsEditingWeekly = false;
        _island.SetClaudeWeeklyReset?.Invoke(reset);
    }

    partial void OnWeeklyTimeTextChanged(string value) => IsWeeklyInvalid = false;

    partial void OnWeeklyDayIndexChanged(int value) => IsWeeklyInvalid = false;

    /// <summary>The row's click: the form opens with the reset time Gili has, ready to be typed over.</summary>
    [RelayCommand]
    private void EditReset()
    {
        IsEditingWeekly = false;
        ResetText = _island.ClaudeWindow is { } window
            ? window.ResetsAt.ToLocalTime().ToString(_use24Hour() ? "HH:mm" : "h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
            : "";
        IsResetInvalid = false;
        IsEditingReset = true;
    }

    [RelayCommand]
    private void CancelReset() => IsEditingReset = false;

    /// <summary>Enter or the check: a time within the next 5 hours becomes the window's reset; any other stays, in red.</summary>
    [RelayCommand]
    public void SaveReset()
    {
        if (ClaudeUsage.ParseReset(ResetText, DateTimeOffset.Now) is not { } reset)
        {
            IsResetInvalid = true;
            return;
        }
        IsEditingReset = false;
        _island.SetClaudeReset?.Invoke(reset);
    }

    partial void OnResetTextChanged(string value) => IsResetInvalid = false;

    public void Refresh()
    {
        RefreshWindow();
        var rows = Gili.Core.Claude.ClaudePage.Rows(_island.ClaudeSessions, DateTimeOffset.Now, _use24Hour());
        // The same sessions in the same order only change their text; otherwise the list is built again.
        if (rows.Select(r => r.Id).SequenceEqual(Rows.Select(r => r.Id)))
        {
            for (var i = 0; i < rows.Count; i++) Rows[i].Row = rows[i];
        }
        else
        {
            Rows.Clear();
            foreach (var row in rows) Rows.Add(new ClaudeRowViewModel(row, _island.OpenClaudeSession));
            IsEmpty = rows.Count == 0 && !HasWindow;
            ContentChanged?.Invoke();
        }
        IsEmpty = rows.Count == 0 && !HasWindow;
        _second.IsEnabled = _shown && rows.Any(r => r.Kind == ClaudeRowKind.Work);
    }
}

/// <summary>One session's row; its look follows <see cref="ClaudeRow.Kind"/>.</summary>
public sealed partial class ClaudeRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kind), nameof(Name), nameof(Sub), nameof(Right), nameof(Tooltip), nameof(IsDone), nameof(Pulses))]
    private ClaudeRow _row;

    public ClaudeRowViewModel(ClaudeRow row, Action<string> open)
    {
        _row = row;
        OpenCommand = new RelayCommand(() => open(Row.Id));
    }

    public string Id => Row.Id;

    public ClaudeRowKind Kind => Row.Kind;

    public string Name => Row.Name;

    public string Sub => Row.Sub;

    public string Right => Row.Right;

    public string Tooltip => Row.Tooltip;

    /// <summary>A finished session shows a check instead of Claude's glyph.</summary>
    public bool IsDone => Row.Kind == ClaudeRowKind.Done;

    /// <summary>A working session's glyph breathes, unless Windows animations are off (PRD 8.3).</summary>
    public bool Pulses => Row.Kind == ClaudeRowKind.Work && System.Windows.SystemParameters.ClientAreaAnimation;

    public IRelayCommand OpenCommand { get; }
}
