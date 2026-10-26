using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using TopBar.Core.Formatting;
using TopBar.Core.Island;
using TopBar.Core.Layout;
using TopBar.Core.Media;

namespace TopBar.App.Island;

public sealed record IslandTab(IslandPage Page, string Name, Geometry Glyph, string? Placeholder);

public sealed partial class IslandViewModel : ObservableObject
{
    private const string Phase2 = "Coming in Phase 2";
    private const string Phase3 = "Coming in Phase 3";
    private static readonly Brush Accent = Frozen("#30D158");
    private static readonly Brush VideoOrange = Frozen("#FF9F0A");
    private static readonly Brush Text = Frozen("#F5F5F7");

    private readonly IslandController _island;
    private readonly ClockService _clock;
    private readonly SettingsHub _settings;
    private byte[]? _coverBytes;
    private string? _levelColor;

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private double _fontSize = 12;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClock), nameof(ShowMediaSlot), nameof(ShowChargingSlot))]
    private ActivityKind? _slot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClock), nameof(ShowMediaSlot))]
    private bool _hasMedia;

    [ObservableProperty] private bool _isMusic;
    [ObservableProperty] private bool _isVideo;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _musicPlaying;
    [ObservableProperty] private string _mediaSlotTitle = "";
    [ObservableProperty] private string _mediaTitle = "";
    [ObservableProperty] private string _mediaArtist = "";
    [ObservableProperty] private string _mediaApp = "";
    [ObservableProperty] private Brush _levelBrush = Accent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private ImageSource? _cover;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _positionText = "";
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private string _remainingText = "";
    [ObservableProperty] private bool _hasTimeline;
    [ObservableProperty] private bool _canSeek;
    [ObservableProperty] private bool _canSkip;
    [ObservableProperty] private string _chargePercent = "";
    [ObservableProperty] private string _wideText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWideValue))]
    private string? _wideValue;

    [ObservableProperty] private bool _wideHasIcon;
    [ObservableProperty] private Brush _wideIconBrush = Text;
    [ObservableProperty] private Geometry? _bubbleGlyph;
    [ObservableProperty] private Brush _bubbleBrush = Text;
    [ObservableProperty] private bool _bubbleFilled;
    [ObservableProperty] private int _selectedTab;

    public IslandViewModel(IslandController island, ClockService clock, SettingsHub settings)
    {
        _island = island;
        _clock = clock;
        _settings = settings;
        // The Claude page joins in Phase 3, when its integration exists.
        Tabs =
        [
            new(IslandPage.Media, "Media", Glyph("Icon.Music"), null),
            new(IslandPage.Timer, "Timer", Glyph("Icon.Hourglass"), Phase2),
            new(IslandPage.Alarms, "Alarms", Glyph("Icon.Alarm"), Phase2),
            new(IslandPage.Calendar, "Calendar", Glyph("Icon.Calendar"), Phase2),
            new(IslandPage.Todo, "Todo", Glyph("Icon.Todo"), Phase2),
            new(IslandPage.Notes, "Notes", Glyph("Icon.Note"), Phase2),
            new(IslandPage.Shelf, "Shelf", Glyph("Icon.Tray"), Phase3),
            new(IslandPage.System, "System", Glyph("Icon.Cpu"), Phase3),
        ];
        clock.Ticked += UpdateClock;
        settings.Changed += _ => UpdateSettings();
        island.ViewChanged += UpdateView;
        island.PowerChanged += UpdatePower;
        island.Media.Updated += UpdateMedia;
        UpdateSettings();
        UpdatePower();
        UpdateMedia();
        UpdateView(island.View);
    }

    public IReadOnlyList<IslandTab> Tabs { get; }

    public bool ShowClock => Slot is not (ActivityKind.Media or ActivityKind.Charging) || (Slot == ActivityKind.Media && !HasMedia);

    public bool ShowMediaSlot => Slot == ActivityKind.Media && HasMedia;

    public bool ShowChargingSlot => Slot == ActivityKind.Charging;

    public bool HasCover => Cover is not null;

    public bool HasWideValue => WideValue is not null;

    private void UpdateClock()
    {
        Time = ClockText.Time(_clock.Now, _settings.Current.Use24HourClock);
        Date = ClockText.Date(_clock.Now);
    }

    private void UpdateSettings()
    {
        FontSize = BarMetrics.IslandFontSize(_settings.Current.IslandHeight);
        UpdateClock();
    }

    private void UpdatePower() =>
        ChargePercent = _island.Power is { } power ? power.Percent.ToString(CultureInfo.InvariantCulture) + "%" : "";

    private void UpdateView(IslandView view)
    {
        Slot = view.Slot;
        var index = Tabs.ToList().FindIndex(t => t.Page == view.Page);
        SelectedTab = index >= 0 ? index : 0;
        if (view.Wide is { } wide)
        {
            WideText = wide.Text;
            WideValue = wide.Value;
            WideHasIcon = wide.Icon != WideIcon.None;
            WideIconBrush = wide.Icon == WideIcon.Charging ? Accent : Text;
        }
        UpdateBubble();
    }

    private void UpdateBubble()
    {
        (BubbleGlyph, BubbleBrush, BubbleFilled) = _island.View.Bubble switch
        {
            ActivityKind.Media => (Glyph(IsVideo ? "Icon.Film" : "Icon.Music"), IsVideo ? VideoOrange : Accent, false),
            ActivityKind.Charging => (Glyph("Icon.Bolt"), Accent, true),
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
        MediaSlotTitle = MediaRules.SlotTitle(media);
        MediaTitle = media.Title;
        MediaArtist = media.Artist;
        MediaApp = MediaRules.AppName(media.AppId);
        var color = MediaRules.LevelColor(media.AppId);
        if (color != _levelColor)
        {
            _levelColor = color;
            LevelBrush = Frozen(color);
        }
        HasTimeline = media.HasTimeline;
        Progress = MediaRules.Progress(position, media.Duration);
        PositionText = MediaRules.Time(position);
        DurationText = MediaRules.Time(media.Duration);
        RemainingText = MediaRules.Remaining(position, media.Duration);
        CanSeek = media.CanSeek && media.HasTimeline;
        CanSkip = media.CanSkip;
        if (!ReferenceEquals(media.Cover, _coverBytes))
        {
            _coverBytes = media.Cover;
            Cover = Decode(media.Cover);
        }
        UpdateBubble();
    }

    private static ImageSource? Decode(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 224;
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

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
