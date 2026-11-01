using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Clipboard;
using Gili.Core.FileOps;
using Gili.Core.Island;
using Gili.Core.Layout;
using Gili.Core.Media;

namespace Gili.App.Island;

public sealed record IslandTab(IslandPage Page, string Name, Geometry Glyph, string? Placeholder);

public sealed partial class IslandViewModel : ObservableObject
{
    private const string Phase3 = "Coming in Phase 3";
    private const string Phase4 = "Coming in Phase 4";
    private static readonly Color FallbackGlow = Color.FromRgb(0x30, 0xD1, 0x58);
    private static readonly Brush Accent = Frozen("#30D158");
    private static readonly Brush VideoOrange = Frozen("#FF9F0A");
    private static readonly Brush Text = Frozen("#F5F5F7");
    private static readonly Brush Blue = Frozen("#0A84FF");
    private static readonly Brush Warn = Frozen("#FFD60A");
    private static readonly Brush Danger = Frozen("#FF453A");
    private static readonly Brush Muted = Frozen("#8E8E93");
    private static readonly Brush ClaudeOrange = Frozen("#D97757");
    private static readonly Brush ShelfBlue = Frozen("#64D2FF");

    private readonly IslandController _island;
    private readonly SettingsHub _settings;
    private byte[]? _coverBytes;
    private bool _coverForVideo;

    /// <summary>The slot's picture in pixels: the artwork square, or a video's wider thumbnail, at twice its size.</summary>
    private int SlotPixels => (int)(IsVideo ? ThumbWidth : ArtworkSize) * 2;
    private string? _levelColor;

    [ObservableProperty] private double _fontSize = 12;
    [ObservableProperty] private int _islandHeight = 24;
    [ObservableProperty] private double _artworkSize = 18;

    /// <summary>A video's thumbnail in the slot is 16:9 at the artwork's height: 32 × 18 px on a 24 px island (issue #31).</summary>
    [ObservableProperty] private double _thumbWidth = 32;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMediaSlot), nameof(ShowChargingSlot), nameof(ShowClipboardSlot), nameof(ShowTimerSlot), nameof(ShowClaudeSlot), nameof(ShowUsageSlot), nameof(ShowShelfSlot), nameof(ShowFileOpSlot), nameof(HasActivity))]
    private ActivityKind? _slot;

    /// <summary>
    /// Explorer's operation in the slot (F-ISL-15, mockup slotHTML 'explorer'): its coloured icon, the item's name, a 44 px
    /// progress line and the percent; then a check and Copied / Moved / Deleted, a grey Cancelled, or Needs attention.
    /// </summary>
    [ObservableProperty] private Geometry? _fileOpGlyph;
    [ObservableProperty] private Brush _fileOpBrush = Accent;
    [ObservableProperty] private string _fileOpLabel = "";
    [ObservableProperty] private string _fileOpEnd = "";
    [ObservableProperty] private Brush _fileOpEndBrush = Text;
    [ObservableProperty] private double _fileOpProgress;
    [ObservableProperty] private bool _fileOpRunning;
    [ObservableProperty] private string? _fileOpTooltip;

    public bool ShowFileOpSlot => Slot == ActivityKind.FileOperation;

    private static (string Glyph, Brush Brush) FileOpLook(FileOpKind kind) => kind switch
    {
        FileOpKind.Copy => ("Icon.Folder", Accent),
        FileOpKind.Move => ("Icon.Scissors", Blue),
        FileOpKind.Delete => ("Icon.Trash", Danger),
        _ => ("Icon.Files", Muted),
    };

    private void UpdateFileOps()
    {
        if (_island.FileOps?.Slot is not { } op)
        {
            UpdateBubble();
            return;
        }
        var (glyph, brush) = FileOpLook(op.Kind);
        (FileOpGlyph, FileOpBrush, FileOpEndBrush) = op.Phase switch
        {
            FileOpPhase.Done => (Glyph("Icon.Check"), brush, Muted),
            FileOpPhase.Cancelled => (Glyph("Icon.Close"), Muted, Muted),
            FileOpPhase.Attention => (Glyph("Icon.Alert"), Warn, Warn),
            _ => (Glyph(glyph), brush, Text),
        };
        FileOpLabel = op.Label;
        FileOpRunning = op.Phase == FileOpPhase.Running;
        FileOpProgress = op.Percent / 100.0;
        FileOpEnd = op.Phase == FileOpPhase.Running
            ? op.Percent.ToString(CultureInfo.InvariantCulture) + "%"
            : FileOpRules.Result(op.Kind, op.Phase);
        FileOpTooltip = op.Tooltip;
        UpdateBubble();
    }

    /// <summary>Claude's 5-hour window in the slot (mockup slotHTML 'lim'): the hourglass, Resets in or Limit reached, the time.</summary>
    [ObservableProperty] private Brush _usageBrush = ClaudeOrange;
    [ObservableProperty] private string _usageLabel = "";
    [ObservableProperty] private string _usageLeft = "";
    [ObservableProperty] private string _usageTooltip = "";

    [ObservableProperty] private Geometry? _claudeGlyph;
    [ObservableProperty] private bool _claudeFilled = true;
    [ObservableProperty] private Brush _claudeBrush = ClaudeOrange;
    [ObservableProperty] private string _claudeLeft = "";
    [ObservableProperty] private string _claudeRight = "";
    [ObservableProperty] private Brush _claudeRightBrush = Muted;
    [ObservableProperty] private string _claudeTooltip = "";
    [ObservableProperty] private bool _claudePulse;
    /// <summary>Another session finished unseen while Claude shows: a green dot on its icon (mockup .clp.dot).</summary>
    [ObservableProperty] private bool _claudeDot;
    [ObservableProperty] private bool _bubbleDot;

    /// <summary>Claude's green bubble (F-CLD-04, mockup renderCbub): the count when more than one finished unseen.</summary>
    [ObservableProperty] private string _doneCount = "";
    [ObservableProperty] private bool _hasDoneCount;
    [ObservableProperty] private string _doneTooltip = "";
    [ObservableProperty] private string _doneName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMediaSlot), nameof(HasActivity))]
    private bool _hasMedia;

    [ObservableProperty] private bool _isMusic;
    [ObservableProperty] private bool _isVideo;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _musicPlaying;
    [ObservableProperty] private string _mediaSlotTitle = "";

    /// <summary>The slot shows the line being sung instead of the title (F-ISL-26); its tooltip holds the whole line.</summary>
    [ObservableProperty] private bool _isLyricLine;
    [ObservableProperty] private string? _mediaSlotTooltip;

    /// <summary>The Media page's lyrics block: Show lyrics is on and music plays (PRD 6.3 "Lirik lagu").</summary>
    [ObservableProperty] private bool _lyricsOn;
    [ObservableProperty] private bool _showLyricsBlock;
    [ObservableProperty] private string _lyricPrevious = "";
    [ObservableProperty] private string _lyricCurrent = "";
    [ObservableProperty] private string _lyricNext = "";

    /// <summary>The middle line is a message (searching, none, offline) rather than a sung line.</summary>
    [ObservableProperty] private bool _lyricIsMessage;

    /// <summary>Raised when a new line starts, so the views can roll it in.</summary>
    public event Action? LyricRolled;
    [ObservableProperty] private string _mediaTitle = "";
    [ObservableProperty] private string _mediaArtist = "";
    [ObservableProperty] private string _mediaApp = "";
    [ObservableProperty] private Brush _levelBrush = Accent;
    [ObservableProperty] private Color _glowPrimary = FallbackGlow;
    [ObservableProperty] private Color _glowSecondary = FallbackGlow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private ImageSource? _cover;

    /// <summary>The track's artwork at the slot's size (twice that in pixels, for high DPI).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtwork))]
    private ImageSource? _artwork;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _positionText = "";
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private string _remainingText = "";
    [ObservableProperty] private bool _hasTimeline;
    [ObservableProperty] private bool _canSeek;
    [ObservableProperty] private bool _canSkip;
    [ObservableProperty] private string _chargePercent = "";
    /// <summary>The wide notice's text pieces; the window lays them out (plan 3a decision 1).</summary>
    [ObservableProperty] private IReadOnlyList<WideRun> _wideRuns = [];
    [ObservableProperty] private string _wideName = "";
    [ObservableProperty] private bool _wideHasIcon;
    [ObservableProperty] private Geometry? _wideGlyph;
    [ObservableProperty] private bool _wideFilled;
    [ObservableProperty] private Brush _wideIconBrush = Text;
    [ObservableProperty] private Geometry? _bubbleGlyph;
    [ObservableProperty] private Brush _bubbleBrush = Text;
    [ObservableProperty] private bool _bubbleFilled;
    [ObservableProperty] private bool _bubbleIsMusic;
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private Geometry? _clipGlyph;
    [ObservableProperty] private Brush _clipBrush = Text;
    [ObservableProperty] private string _clipText = "";
    [ObservableProperty] private string? _clipDetail;
    [ObservableProperty] private string _clipTooltip = "";
    [ObservableProperty] private int _clipTextColumn = 4;

    /// <summary>Files waiting on the Shelf (issue 37): the newest item's name or the file count, and the total size.</summary>
    [ObservableProperty] private string _shelfSlotText = "";
    [ObservableProperty] private string? _shelfSlotDetail;
    [ObservableProperty] private string _shelfSlotTooltip = "";
    [ObservableProperty] private int _shelfSlotTextColumn = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipThumb))]
    private ImageSource? _clipThumb;

    [ObservableProperty] private Geometry? _timerSlotGlyph;
    [ObservableProperty] private Brush _timerSlotBrush = TimerViewModel.Orange;
    [ObservableProperty] private string _timerSlotValue = "";
    [ObservableProperty] private Geometry? _cardGlyph;
    [ObservableProperty] private Brush _cardBrush = Text;
    [ObservableProperty] private Brush _cardTint = Text;
    [ObservableProperty] private string _cardTitle = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCardSubtitle))]
    private string? _cardSubtitle;
    [ObservableProperty] private IReadOnlyList<CardButton> _cardButtons = [];
    [ObservableProperty] private bool _cardShake;
    [ObservableProperty] private bool _cardFilled;
    [ObservableProperty] private bool _cardPulse = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCardQuote))]
    private string? _cardQuote;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCardWarning))]
    private string? _cardWarning;

    public IslandViewModel(IslandController island, SettingsHub settings)
    {
        _island = island;
        _settings = settings;
        Tabs =
        [
            new(IslandPage.Media, "Media", Glyph("Icon.Music"), null),
            new(IslandPage.Timer, "Timer", Glyph("Icon.Hourglass"), null),
            new(IslandPage.Alarms, "Alarms", Glyph("Icon.Alarm"), null),
            new(IslandPage.Calendar, "Calendar", Glyph("Icon.Calendar"), null),
            new(IslandPage.Todo, "Todo", Glyph("Icon.Todo"), null),
            new(IslandPage.Notes, "Notes", Glyph("Icon.Note"), null),
            new(IslandPage.Clipboard, "Clipboard", Glyph("Icon.Clipboard"), null),
            new(IslandPage.Shelf, "Shelf", Glyph("Icon.Tray"), null),
            new(IslandPage.Claude, "Claude", Glyph("Icon.Claude"), null),
            new(IslandPage.System, "System", Glyph("Icon.Cpu"), null),
        ];
        settings.Changed += _ =>
        {
            UpdateSettings();
            _island.Lyrics.Sync();
        };
        island.ViewChanged += UpdateView;
        island.PowerChanged += UpdatePower;
        island.Media.Updated += UpdateMedia;
        island.FileOpsChanged += UpdateFileOps;
        island.Lyrics.Changed += UpdateLyrics;
        island.Lyrics.LineChanged += () =>
        {
            UpdateLyrics();
            LyricRolled?.Invoke();
        };
        island.ClipboardChanged += UpdateClipboard;
        island.Shelf.Changed += UpdateShelfSlot;
        island.Timers.Changed += UpdateTimerSlot;
        island.ClaudeChanged += UpdateClaude;
        island.ClaudeWindowTick += UpdateUsage;
        island.CardChanged += () => UpdateCard(_island.View);
        UpdateSettings();
        UpdatePower();
        UpdateMedia();
        UpdateShelfSlot();
        UpdateView(island.View);
    }

    public IReadOnlyList<IslandTab> Tabs { get; }

    /// <summary>The tabs the menu shows: hidden ones leave, a hidden page opened directly shows while open (F-ISL-23).</summary>
    [ObservableProperty] private IReadOnlyList<IslandPage> _visiblePages = IslandTabs.Menu;

    private void UpdateVisiblePages(IslandPage page)
    {
        var hidden = _settings.Current.HiddenTabs;
        IslandPage? temporary = page != IslandPage.Controls && hidden.HasFlag(IslandTabs.Of(page)) ? page : null;
        var pages = IslandTabs.Visible(hidden, temporary);
        if (!pages.SequenceEqual(VisiblePages)) VisiblePages = pages;
    }

    public bool ShowMediaSlot => Slot == ActivityKind.Media && HasMedia;

    public bool ShowChargingSlot => Slot == ActivityKind.Charging;

    public bool ShowClipboardSlot => Slot == ActivityKind.ClipboardSnippet;

    public bool ShowTimerSlot => Slot is ActivityKind.Timer or ActivityKind.Stopwatch;

    public bool ShowClaudeSlot => Slot is ActivityKind.ClaudeAttention or ActivityKind.ClaudeWorking;

    public bool ShowUsageSlot => Slot is ActivityKind.ClaudeLimit or ActivityKind.ClaudeWindow;

    public bool ShowShelfSlot => Slot == ActivityKind.Shelf;

    public bool HasCardSubtitle => !string.IsNullOrEmpty(CardSubtitle);

    public bool HasCardWarning => CardWarning is not null;

    public bool HasCardQuote => !string.IsNullOrEmpty(CardQuote);

    /// <summary>Something holds the slot; otherwise the idle island shows Gili (PRD 6.3).</summary>
    public bool HasActivity => ShowMediaSlot || ShowChargingSlot || ShowClipboardSlot || ShowTimerSlot || ShowClaudeSlot || ShowUsageSlot || ShowShelfSlot || ShowFileOpSlot;

    /// <summary>
    /// The Claude slot (mockup clSlotHTML): yellow Claude glyph and Needs input, green check and Done · 4m 12s, or the
    /// orange glyph pulsing with the running time; several sessions show their count instead of a name.
    /// </summary>
    private void UpdateClaude()
    {
        var claude = _island.Claude;
        var done = claude.State == Gili.Core.Claude.ClaudeSlotState.Done;
        ClaudeGlyph = Glyph(done ? "Icon.Check" : "Icon.Claude");
        ClaudeFilled = !done;
        ClaudeBrush = claude.State switch
        {
            Gili.Core.Claude.ClaudeSlotState.NeedsInput => Warn,
            Gili.Core.Claude.ClaudeSlotState.Done => Accent,
            _ => ClaudeOrange,
        };
        ClaudeLeft = claude.Name ?? claude.Text;
        ClaudeRight = claude.Name is null ? "" : claude.State == Gili.Core.Claude.ClaudeSlotState.Working ? claude.Value ?? "" : claude.Text;
        ClaudeRightBrush = claude.State == Gili.Core.Claude.ClaudeSlotState.NeedsInput ? Warn : Muted;
        ClaudeTooltip = claude.Tooltip;
        ClaudePulse = claude.State == Gili.Core.Claude.ClaudeSlotState.Working;
        ClaudeDot = claude.Dot;
        var unseen = _island.ClaudeUnseen;
        DoneCount = unseen.Count.ToString(CultureInfo.InvariantCulture);
        HasDoneCount = unseen.Count > 1;
        (DoneTooltip, DoneName) = Gili.Core.Claude.ClaudeSlot.Bubble(unseen);
        UpdateUsage();
        UpdateBubble();
    }

    /// <summary>
    /// The slot for a timer, pomodoro or stopwatch (mockup ACT_ICON): an hourglass in orange (timer), red (focus) or green
    /// (break), or an orange stopwatch, and the time at the right end. The pomodoro wins when both a timer and it run.
    /// </summary>
    private void UpdateTimerSlot()
    {
        var timers = _island.Timers;
        var now = DateTimeOffset.UtcNow;
        if (Slot == ActivityKind.Stopwatch || (Slot != ActivityKind.Timer && _island.View.Bubble == ActivityKind.Stopwatch))
        {
            TimerSlotGlyph = Glyph("Icon.Stopwatch");
            TimerSlotBrush = TimerViewModel.Orange;
            TimerSlotValue = Gili.Core.Timers.TimerText.Stopwatch(timers.Stopwatch.Elapsed(now), tenths: false);
        }
        if (Slot == ActivityKind.Timer)
        {
            var pomodoro = timers.Pomodoro.Clock.IsRunning;
            TimerSlotGlyph = Glyph("Icon.Hourglass");
            TimerSlotBrush = !pomodoro ? TimerViewModel.Orange
                : timers.Pomodoro.Phase == Gili.Core.Timers.PomodoroPhase.Focus ? TimerViewModel.Red : TimerViewModel.Green;
            var clock = pomodoro ? timers.Pomodoro.Clock : timers.SoonestTimer ?? timers.Timer;
            TimerSlotValue = Gili.Core.Timers.TimerText.Clock(clock.Remaining(now));
        }
        UpdateBubble();
    }

    private void UpdateUsage()
    {
        if (_island.ClaudeWindow is not { } window) return;
        var view = Gili.Core.Claude.ClaudeUsageText.For(window, DateTimeOffset.Now, _settings.Current.Use24HourClock);
        UsageBrush = view.LimitHit ? Warn : ClaudeOrange;
        UsageLabel = view.Label;
        UsageLeft = view.Left;
        UsageTooltip = view.Tooltip;
    }

    private void UpdateCard(IslandView view)
    {
        if (view.Card is not { } card || _island.CardFor(card.Id) is not { } content) return;
        CardGlyph = Glyph(content.Glyph);
        CardBrush = Frozen(content.Color);
        CardTint = Frozen(Color.FromArgb(0x38, content.Color.R, content.Color.G, content.Color.B));
        CardTitle = content.Title;
        CardSubtitle = content.Subtitle;
        CardButtons = content.Buttons;
        CardWarning = content.Warning;
        CardShake = content.Shake;
        CardQuote = content.Quote;
        CardFilled = content.Filled;
        CardPulse = content.Pulse;
    }

    public bool HasClipThumb => ClipThumb is not null;

    /// <summary>
    /// A copy's line (mockup <c>ACT.copy*</c>): clipboard, file, blue scissors or lock icon, or the image's thumbnail;
    /// the text sits at the right end, or in the middle when an image's size follows it.
    /// </summary>
    private void UpdateClipboard()
    {
        if (_island.Clipboard is not var (snippet, thumb)) return;
        ClipGlyph = Glyph(snippet.Kind switch
        {
            SnippetKind.Files or SnippetKind.Image => "Icon.File",
            SnippetKind.Cut => "Icon.Scissors",
            SnippetKind.Hidden => "Icon.Lock",
            _ => "Icon.Copied",
        });
        ClipBrush = snippet.Kind == SnippetKind.Cut ? Blue : Text;
        ClipThumb = thumb;
        ClipText = snippet.Text;
        ClipDetail = snippet.Detail;
        ClipTextColumn = snippet.Detail is null ? 4 : 2;
        ClipTooltip = snippet.Tooltip;
    }

    private void UpdateShelfSlot()
    {
        var shelf = _island.Shelf;
        if (shelf.Items.Count == 0) return;
        (ShelfSlotText, ShelfSlotDetail) = Gili.Core.Shelf.ShelfText.Slot(shelf.Items);
        ShelfSlotTextColumn = ShelfSlotDetail is null ? 4 : 2;
        ShelfSlotTooltip = Gili.Core.Shelf.ShelfText.BarTooltip(shelf.FileCount);
    }

    public bool HasCover => Cover is not null;

    public bool HasArtwork => Artwork is not null;

    private void UpdateSettings()
    {
        UpdateVisiblePages(_island.View.Page);
        var height = _settings.Current.IslandHeight;
        FontSize = BarMetrics.IslandFontSize(height);
        IslandHeight = height;
        if (ArtworkSize != height - 6)
        {
            ArtworkSize = height - 6;
            ThumbWidth = Math.Round(ArtworkSize * 16 / 9);
            Artwork = Decode(_coverBytes, SlotPixels);
        }
    }

    private void UpdatePower() =>
        ChargePercent = _island.Power is { } power ? power.Percent.ToString(CultureInfo.InvariantCulture) + "%" : "";

    private void UpdateView(IslandView view)
    {
        Slot = view.Slot;
        UpdateTimerSlot();
        UpdateCard(view);
        UpdateVisiblePages(view.Page);
        // The control island has no tab; the menu keeps its page for when a page opens again.
        var index = Tabs.ToList().FindIndex(t => t.Page == view.Page);
        if (index >= 0) SelectedTab = index;
        if (view.Wide is { } wide)
        {
            WideName = wide.Text;
            WideHasIcon = wide.Icon != WideIcon.None;
            WideGlyph = wide.Icon == WideIcon.None ? null : Glyph(WideGlyphKey(wide.Icon));
            WideFilled = wide.Icon == WideIcon.Bolt;
            WideIconBrush = ToneBrush(wide.Tone);
            WideRuns = wide.Runs;
        }
        UpdateBubble();
    }

    private void UpdateBubble()
    {
        // Music as the second activity shows its artwork in the bubble instead of a note glyph, and a video its thumbnail.
        BubbleIsMusic = _island.View.Bubble == ActivityKind.Media && (IsMusic || HasArtwork);
        BubbleDot = _island.View.Bubble is ActivityKind.ClaudeAttention or ActivityKind.ClaudeWorking && _island.Claude.Dot;
        (BubbleGlyph, BubbleBrush, BubbleFilled) = _island.View.Bubble switch
        {
            ActivityKind.Media => (Glyph(IsVideo ? "Icon.Film" : "Icon.Music"), IsVideo ? VideoOrange : Accent, false),
            ActivityKind.Charging => (Glyph("Icon.Bolt"), Accent, true),
            ActivityKind.Shelf => (Glyph("Icon.Tray"), ShelfBlue, false),
            ActivityKind.FileOperation when _island.FileOps?.Slot is { } op => (Glyph(FileOpLook(op.Kind).Glyph), FileOpLook(op.Kind).Brush, false),
            ActivityKind.Timer => (Glyph("Icon.Hourglass"), _island.Timers.Pomodoro.Clock.IsRunning
                ? (_island.Timers.Pomodoro.Phase == Gili.Core.Timers.PomodoroPhase.Focus ? TimerViewModel.Red : TimerViewModel.Green)
                : TimerViewModel.Orange, false),
            ActivityKind.Stopwatch => (Glyph("Icon.Stopwatch"), TimerViewModel.Orange, false),
            ActivityKind.ClaudeAttention when _island.Claude.State == Gili.Core.Claude.ClaudeSlotState.Done => (Glyph("Icon.Check"), Accent, false),
            ActivityKind.ClaudeAttention => (Glyph("Icon.Claude"), Warn, true),
            ActivityKind.ClaudeWorking => (Glyph("Icon.Claude"), ClaudeOrange, true),
            ActivityKind.ClaudeLimit => (Glyph("Icon.Hourglass"), Warn, false),
            ActivityKind.ClaudeWindow => (Glyph("Icon.Hourglass"), ClaudeOrange, false),
            _ => ((Geometry?)null, Text, false),
        };
    }

    private void UpdateMedia()
    {
        var media = _island.Media.Current;
        HasMedia = media is not null;
        if (media is null)
        {
            IsMusic = false;
            IsVideo = false;
            IsPlaying = false;
            MusicPlaying = false;
            UpdateBubble();
            return;
        }
        var position = media.PositionAt(DateTimeOffset.Now);
        IsVideo = media.Kind == MediaKind.Video;
        IsMusic = !IsVideo;
        IsPlaying = media.IsPlaying;
        MusicPlaying = media.IsPlaying && IsMusic;
        UpdateSlotTitle(media);
        MediaTitle = media.Title;
        MediaArtist = media.Artist;
        MediaApp = MediaRules.AppName(media.AppId);
        if (!ReferenceEquals(media.Cover, _coverBytes) || IsVideo != _coverForVideo)
        {
            _coverBytes = media.Cover;
            _coverForVideo = IsVideo;
            // A video's 16:9 thumbnail is 199 px wide on the Media page, twice that for high DPI.
            Cover = Decode(media.Cover, IsVideo ? 400 : 224);
            Artwork = Decode(media.Cover, SlotPixels);
            _levelColor = null;
        }
        // The artwork's main colour tints the level meter, the source dot and the glow; the app's colour without artwork.
        var palette = Cover is BitmapSource cover ? Palette(cover) : null;
        var color = palette?.Primary.Hex ?? MediaRules.LevelColor(media.AppId);
        if (color != _levelColor)
        {
            _levelColor = color;
            LevelBrush = Frozen(color);
            GlowPrimary = (Color)ColorConverter.ConvertFromString(color);
            GlowSecondary = palette is { } p ? (Color)ColorConverter.ConvertFromString(p.Secondary.Hex) : GlowPrimary;
        }
        HasTimeline = media.HasTimeline;
        Progress = MediaRules.Progress(position, media.Duration);
        PositionText = MediaRules.Time(position);
        DurationText = MediaRules.Time(media.Duration);
        RemainingText = MediaRules.Remaining(position, media.Duration);
        CanSeek = media.CanSeek && media.HasTimeline;
        CanSkip = media.CanSkip;
        UpdateBubble();
    }

    private void UpdateSlotTitle(MediaState media)
    {
        var line = _island.Lyrics.SlotLine;
        IsLyricLine = line is not null;
        MediaSlotTitle = line ?? MediaRules.SlotTitle(media);
        MediaSlotTooltip = line is null ? null : _island.Lyrics.Lines?[_island.Lyrics.Index].Text;
    }

    private void UpdateLyrics()
    {
        var lyrics = _island.Lyrics;
        LyricsOn = _settings.Current.ShowLyrics;
        ShowLyricsBlock = lyrics.Shown && IsMusic;
        if (lyrics.Status == Gili.Core.Lyrics.LyricsStatus.Synced && lyrics.Lines is { } lines)
        {
            LyricIsMessage = false;
            (LyricPrevious, LyricCurrent, LyricNext) = Gili.Core.Lyrics.LyricsView.Window(lines, lyrics.Index);
        }
        else
        {
            LyricIsMessage = true;
            (LyricPrevious, LyricCurrent, LyricNext) = ("", Gili.Core.Lyrics.LyricsView.Message(lyrics.Status), "");
        }
        if (_island.Media.Current is { } media) UpdateSlotTitle(media);
    }

    private (Rgb Primary, Rgb Secondary)? _palette;
    private BitmapSource? _paletteOf;

    /// <summary>Worked out once per artwork (PRD 8.1), from the 224 px cover the page already holds.</summary>
    private (Rgb Primary, Rgb Secondary)? Palette(BitmapSource cover)
    {
        if (ReferenceEquals(cover, _paletteOf)) return _palette;
        _paletteOf = cover;
        var pixels = new FormatConvertedBitmap(cover, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[pixels.PixelWidth * pixels.PixelHeight * 4];
        pixels.CopyPixels(bytes, pixels.PixelWidth * 4, 0);
        _palette = ArtworkPalette.From(bytes, pixels.PixelWidth, pixels.PixelHeight);
        return _palette;
    }

    private static ImageSource? Decode(byte[]? bytes, int width)
    {
        if (bytes is not { Length: > 0 }) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = width;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException)
        {
            // An app sent cover bytes WPF cannot decode; the page shows the music icon instead.
            return null;
        }
    }

    private static Geometry Glyph(string key) => (Geometry)Application.Current.FindResource(key);

    private static string WideGlyphKey(WideIcon icon) => "Icon." + icon;

    /// <summary>PRD 8.3 tokens for a wide notice's icon.</summary>
    public static Brush ToneBrush(WideTone tone) => tone switch
    {
        WideTone.Accent => Accent,
        WideTone.Warn => Warn,
        WideTone.Danger => Danger,
        WideTone.Blue => Blue,
        WideTone.Muted => Muted,
        _ => Text,
    };

    private static SolidColorBrush Frozen(string hex) => Frozen((Color)ColorConverter.ConvertFromString(hex));

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
