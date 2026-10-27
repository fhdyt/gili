using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Clipboard;
using Gili.Core.Island;
using Gili.Core.Layout;
using Gili.Core.Media;

namespace Gili.App.Island;

public sealed record IslandTab(IslandPage Page, string Name, Geometry Glyph, string? Placeholder);

public sealed partial class IslandViewModel : ObservableObject
{
    private const string Phase2 = "Coming in Phase 2";
    private const string Phase3 = "Coming in Phase 3";
    private const string Phase4 = "Coming in Phase 4";
    private static readonly Color FallbackGlow = Color.FromRgb(0x30, 0xD1, 0x58);
    private static readonly Brush Accent = Frozen("#30D158");
    private static readonly Brush VideoOrange = Frozen("#FF9F0A");
    private static readonly Brush Text = Frozen("#F5F5F7");
    private static readonly Brush Blue = Frozen("#0A84FF");

    private readonly IslandController _island;
    private readonly SettingsHub _settings;
    private byte[]? _coverBytes;
    private string? _levelColor;

    [ObservableProperty] private double _fontSize = 12;
    [ObservableProperty] private int _islandHeight = 24;
    [ObservableProperty] private double _artworkSize = 18;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMediaSlot), nameof(ShowChargingSlot), nameof(ShowClipboardSlot), nameof(HasActivity))]
    private ActivityKind? _slot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMediaSlot), nameof(HasActivity))]
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
    [ObservableProperty] private string _wideText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWideValue))]
    private string? _wideValue;

    [ObservableProperty] private bool _wideHasIcon;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipThumb))]
    private ImageSource? _clipThumb;

    public IslandViewModel(IslandController island, SettingsHub settings)
    {
        _island = island;
        _settings = settings;
        // Until Phase 3 the Claude tab is a placeholder, as in the mockup (PRD 6.3).
        Tabs =
        [
            new(IslandPage.Media, "Media", Glyph("Icon.Music"), null),
            new(IslandPage.Timer, "Timer", Glyph("Icon.Hourglass"), Phase2),
            new(IslandPage.Alarms, "Alarms", Glyph("Icon.Alarm"), Phase2),
            new(IslandPage.Calendar, "Calendar", Glyph("Icon.Calendar"), Phase2),
            new(IslandPage.Todo, "Todo", Glyph("Icon.Todo"), Phase2),
            new(IslandPage.Notes, "Notes", Glyph("Icon.Note"), Phase2),
            new(IslandPage.Clipboard, "Clipboard", Glyph("Icon.Clipboard"), Phase4),
            new(IslandPage.Shelf, "Shelf", Glyph("Icon.Tray"), Phase3),
            new(IslandPage.Claude, "Claude", Glyph("Icon.Claude"), Phase3),
            new(IslandPage.System, "System", Glyph("Icon.Cpu"), Phase3),
        ];
        settings.Changed += _ => UpdateSettings();
        island.ViewChanged += UpdateView;
        island.PowerChanged += UpdatePower;
        island.Media.Updated += UpdateMedia;
        island.ClipboardChanged += UpdateClipboard;
        UpdateSettings();
        UpdatePower();
        UpdateMedia();
        UpdateView(island.View);
    }

    public IReadOnlyList<IslandTab> Tabs { get; }

    public bool ShowMediaSlot => Slot == ActivityKind.Media && HasMedia;

    public bool ShowChargingSlot => Slot == ActivityKind.Charging;

    public bool ShowClipboardSlot => Slot == ActivityKind.ClipboardSnippet;

    /// <summary>Something holds the slot; otherwise the idle island shows Gili (PRD 6.3).</summary>
    public bool HasActivity => ShowMediaSlot || ShowChargingSlot || ShowClipboardSlot;

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

    public bool HasCover => Cover is not null;

    public bool HasArtwork => Artwork is not null;

    public bool HasWideValue => WideValue is not null;

    private void UpdateSettings()
    {
        var height = _settings.Current.IslandHeight;
        FontSize = BarMetrics.IslandFontSize(height);
        IslandHeight = height;
        if (ArtworkSize != height - 6)
        {
            ArtworkSize = height - 6;
            Artwork = Decode(_coverBytes, (int)ArtworkSize * 2);
        }
    }

    private void UpdatePower() =>
        ChargePercent = _island.Power is { } power ? power.Percent.ToString(CultureInfo.InvariantCulture) + "%" : "";

    private void UpdateView(IslandView view)
    {
        Slot = view.Slot;
        // The control island has no tab; the menu keeps its page for when a page opens again.
        var index = Tabs.ToList().FindIndex(t => t.Page == view.Page);
        if (index >= 0) SelectedTab = index;
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
        // Music as the second activity shows its artwork in the bubble instead of a note glyph.
        BubbleIsMusic = _island.View.Bubble == ActivityKind.Media && IsMusic;
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
        if (!ReferenceEquals(media.Cover, _coverBytes))
        {
            _coverBytes = media.Cover;
            Cover = Decode(media.Cover, 224);
            Artwork = Decode(media.Cover, (int)ArtworkSize * 2);
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

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
