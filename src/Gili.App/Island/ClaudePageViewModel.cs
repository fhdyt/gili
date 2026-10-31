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

    /// <summary>The 5-hour window's row above the sessions (mockup limRow): ring, Resets 19:00, the time left.</summary>
    [ObservableProperty] private bool _hasWindow;
    [ObservableProperty] private bool _windowLimitHit;
    [ObservableProperty] private string _windowResets = "";
    [ObservableProperty] private string _windowLeft = "";
    [ObservableProperty] private string _windowTooltip = "";

    /// <summary>The ring's arc for the time left, in stroke widths (the ring is 2.75 px thick on a 51.8 px circle).</summary>
    [ObservableProperty] private System.Windows.Media.DoubleCollection _windowArc = [0, 100];

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
        HasWindow = _island.ClaudeWindow is not null;
        if (_island.ClaudeWindow is { } window)
        {
            var view = ClaudeUsageText.For(window, DateTimeOffset.Now, _use24Hour());
            WindowLimitHit = view.LimitHit;
            WindowResets = view.Resets;
            WindowLeft = view.Left;
            WindowTooltip = view.Tooltip;
            const double Circle = 18.85;
            WindowArc = [Math.Max(0.001, Circle * (1 - view.Elapsed)), 100];
        }
        if (hadWindow != HasWindow) ContentChanged?.Invoke();
    }

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
            IsEmpty = rows.Count == 0 && _island.ClaudeWindow is null;
            ContentChanged?.Invoke();
        }
        IsEmpty = rows.Count == 0 && _island.ClaudeWindow is null;
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
