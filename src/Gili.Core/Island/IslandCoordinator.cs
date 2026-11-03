using Gili.Core.Clipboard;

namespace Gili.Core.Island;

/// <summary>
/// Decides what the island shows (PRD 6.3 "Prioritas tampilan"): cards, then the drop zone, then the page the user
/// opened, then wide notices, then running activities, then the clock. Lower priorities wait in a queue.
/// </summary>
public sealed class IslandCoordinator
{
    private readonly Func<DateTimeOffset> _now;
    private readonly HashSet<ActivityKind> _activities = [];
    private readonly List<WideEvent> _wideQueue = [];
    private readonly List<IslandCard> _cards = [];
    private WideEvent? _wide;
    private DateTimeOffset _wideUntil;
    private IslandCard? _shownCard;
    private DateTimeOffset _cardUntil;
    private bool _dropZone;
    private bool _fullscreen;
    private bool _focus;
    private bool _claudeUnseen;
    private DateTimeOffset? _clipboardUntil;
    private IslandPage? _openPage;
    private IslandPage _lastPage = IslandPage.Media;
    private ControlKind _control = ControlKind.Power;
    private IslandPage? _dismissedPage;
    private ControlKind _dismissedControl;
    private DateTimeOffset _dismissedAt;

    /// <summary>How long after a dismiss the same page's icon counts as the click that dismissed it.</summary>
    private static readonly TimeSpan DismissClick = TimeSpan.FromMilliseconds(400);

    public IslandCoordinator(Func<DateTimeOffset> now)
    {
        _now = now;
        View = Compute();
    }

    public IslandView View { get; private set; }

    public event Action<IslandView>? Changed;

    /// <summary>When <see cref="Tick"/> must run for a wide notice or reminder card to end; null when nothing is timed.</summary>
    public DateTimeOffset? NextDeadline
    {
        get
        {
            DateTimeOffset? next = _wide is null ? null : _wideUntil;
            if (_clipboardUntil is { } clip && (next is null || clip < next)) next = clip;
            if (_shownCard is { Priority: CardPriority.Reminder } && (next is null || _cardUntil < next)) next = _cardUntil;
            return next;
        }
    }

    public void SetActivity(ActivityKind kind, bool active)
    {
        if (active ? _activities.Add(kind) : _activities.Remove(kind)) Update();
    }

    /// <summary>A copy takes the slot for 3 s; another copy restarts the 3 s (PRD 6.3 "Cuplikan clipboard").</summary>
    public void ShowClipboard()
    {
        if (_fullscreen) return;
        _clipboardUntil = _now() + ClipboardSnippets.Duration;
        _activities.Add(ActivityKind.ClipboardSnippet);
        Update();
    }

    public void ShowWide(WideEvent wide)
    {
        if (_fullscreen) return;
        _wideQueue.RemoveAll(w => w.Key == wide.Key);
        if (_wide?.Key == wide.Key)
        {
            _wide = wide;
            _wideUntil = _now() + wide.Duration;
        }
        else
        {
            _wideQueue.Add(wide);
        }
        Update();
    }

    public void ShowCard(IslandCard card)
    {
        if (_fullscreen && card.Priority == CardPriority.Reminder) return;
        var index = _cards.FindIndex(c => c.Id == card.Id);
        if (index >= 0)
        {
            _cards[index] = card;
            // New content for the card on screen restarts its time (PRD 6.3: a new question restarts the 8 s).
            if (_shownCard?.Id == card.Id) _shownCard = null;
        }
        else
        {
            _cards.Add(card);
        }
        Update();
    }

    /// <summary>Whether a card waits or shows.</summary>
    public bool HasCard(string id) => _cards.Any(c => c.Id == id);

    public void DismissCard(string id)
    {
        if (_cards.RemoveAll(c => c.Id == id) > 0) Update();
    }

    /// <summary>
    /// A Claude session finished while its window was in the background (F-CLD-04): the green bubble, which is no
    /// activity, so Gili stays in the island.
    /// </summary>
    public void SetClaudeUnseen(bool unseen)
    {
        if (_claudeUnseen == unseen) return;
        _claudeUnseen = unseen;
        Update();
    }

    public void SetDropZone(bool active)
    {
        _dropZone = active;
        Update();
    }

    /// <summary>A fullscreen app hides the bar: only urgent cards still appear; wide notices and reminders are dropped.</summary>
    /// <summary>
    /// A pomodoro focus phase holds priority 2 (reminder cards) until it ends; alarms and timers still come through
    /// (F-TOOL-04). A held reminder gets its full time once it shows.
    /// </summary>
    public void SetFocus(bool focus)
    {
        if (_focus == focus) return;
        _focus = focus;
        Update();
    }

    public void SetFullscreen(bool fullscreen)
    {
        _fullscreen = fullscreen;
        if (fullscreen)
        {
            _openPage = null;
            _wide = null;
            _wideQueue.Clear();
            _cards.RemoveAll(c => c.Priority == CardPriority.Reminder);
        }
        Update();
    }

    public void Open(IslandPage page)
    {
        if (_fullscreen) return;
        _openPage = page;
        // A control island is opened from its bar icon only; clicking the island reopens the last real page.
        if (page != IslandPage.Controls) _lastPage = page;
        Update();
    }

    /// <summary>Opens a control island, or swaps the open one's control without closing (PRD 6.4).</summary>
    public void Open(ControlKind control)
    {
        _control = control;
        Open(IslandPage.Controls);
    }

    public void Close()
    {
        _openPage = null;
        Update();
    }

    /// <summary>
    /// Closes the island because the pointer went down outside it. A bar icon's press does this before its click
    /// arrives, so <see cref="Toggle"/> of the same page just after leaves it closed.
    /// </summary>
    public void Dismiss()
    {
        _dismissedPage = _openPage;
        _dismissedControl = _control;
        _dismissedAt = _now();
        Close();
    }

    /// <summary>Bar icons and slots that open a page toggle it: the page that is already open closes the island.</summary>
    public void Toggle(IslandPage page)
    {
        if (page == IslandPage.Controls)
        {
            Toggle(ControlKind.Power);
            return;
        }
        var dismissedByThisClick = _dismissedPage == page && _now() - _dismissedAt < DismissClick;
        _dismissedPage = null;
        if (dismissedByThisClick) return;
        if (_openPage == page) Close();
        else Open(page);
    }

    /// <summary>A control's bar icon: its own open control closes the island, another control swaps in.</summary>
    public void Toggle(ControlKind control)
    {
        var dismissedByThisClick = _dismissedPage == IslandPage.Controls && _dismissedControl == control && _now() - _dismissedAt < DismissClick;
        _dismissedPage = null;
        if (dismissedByThisClick) return;
        if (_openPage == IslandPage.Controls && _control == control) Close();
        else Open(control);
    }

    /// <summary>Opens the page of an activity (slot or bubble click), or the last page viewed when it has none.</summary>
    public void OpenFor(ActivityKind? kind) => Open(PageFor(kind) ?? _lastPage);

    public void Tick() => Update();

    private static IslandPage? PageFor(ActivityKind? kind) => kind switch
    {
        ActivityKind.Media => IslandPage.Media,
        ActivityKind.Shelf => IslandPage.Shelf,
        ActivityKind.Timer or ActivityKind.Stopwatch => IslandPage.Timer,
        ActivityKind.ClaudeAttention or ActivityKind.ClaudeWorking or ActivityKind.ClaudeLimit or ActivityKind.ClaudeWindow => IslandPage.Claude,
        _ => null,
    };

    /// <summary>Fullscreen and a focus phase hold reminders; a focus phase also holds urgent cards that ask to wait.</summary>
    private bool Shows(IslandCard card) => card.Priority == CardPriority.Urgent ? !(_focus && card.HoldInFocus) : !(_fullscreen || _focus);

    private static bool IsClaude(ActivityKind? kind) => kind is ActivityKind.ClaudeAttention or ActivityKind.ClaudeWorking;

    private void Update()
    {
        var view = Compute();
        if (view == View) return;
        View = view;
        Changed?.Invoke(view);
    }

    private IslandView Compute()
    {
        var now = _now();
        if (_wide is not null && now >= _wideUntil) _wide = null;
        if (_clipboardUntil is { } clip && now >= clip)
        {
            _clipboardUntil = null;
            _activities.Remove(ActivityKind.ClipboardSnippet);
        }
        if (_shownCard is { Priority: CardPriority.Reminder } && now >= _cardUntil)
        {
            _cards.Remove(_shownCard);
            _shownCard = null;
        }

        // Cards outrank everything; within a priority the oldest goes first (OrderBy is stable).
        var card = _cards.Where(Shows).OrderBy(c => c.Priority).FirstOrDefault();
        if (card != _shownCard)
        {
            _shownCard = card;
            if (card is { Priority: CardPriority.Reminder }) _cardUntil = now + card.Timeout;
        }

        IslandMode mode;
        if (_shownCard is not null) mode = IslandMode.Card;
        else if (_fullscreen) mode = IslandMode.Hidden;
        else if (_dropZone) mode = IslandMode.DropZone;
        else if (_openPage is not null) mode = IslandMode.Expanded;
        else
        {
            // A wide notice waits while anything above it is on screen; its time starts when it shows.
            if (_wide is null && _wideQueue.Count > 0)
            {
                _wide = _wideQueue[0];
                _wideQueue.RemoveAt(0);
                _wideUntil = now + _wide.Duration;
            }
            mode = _wide is null ? IslandMode.Compact : IslandMode.Wide;
        }

        var ranked = _activities.Order().ToList();
        ActivityKind? slot = ranked.Count > 0 ? ranked[0] : null;
        ActivityKind? bubble = mode == IslandMode.Compact && ranked.Count > 1 ? ranked[1] : null;
        // While Claude shows in the slot or the bubble, an unseen finish is a dot on its icon instead (PRD 6.3).
        var doneBubble = mode == IslandMode.Compact && _claudeUnseen && !IsClaude(slot) && !IsClaude(bubble);
        return new IslandView(
            mode,
            slot,
            bubble,
            mode == IslandMode.Wide ? _wide : null,
            mode == IslandMode.Card ? _shownCard : null,
            _openPage ?? _lastPage,
            _control,
            doneBubble);
    }
}
