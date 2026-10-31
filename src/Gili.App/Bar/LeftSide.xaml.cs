using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Controls;
using Gili.Core.Apps;
using Gili.Core.Island;
using Gili.Core.Motion;
using Gili.Core.Settings;
using Gili.Core.Shelf;
using Gili.Platform.Shell;

namespace Gili.App.Bar;

/// <summary>
/// The left of the bar (PRD 6.2): Start, the launcher with Shelf and Claude, a divider, and the running apps — each by
/// its own icon, the active one named in a capsule, a hovered one named while the pointer is on it, and the ones that
/// don't fit gathered in "+N".
/// </summary>
public partial class LeftSide : UserControl
{
    private const double Gap = 12;
    private const double Divider = 1;
    private static readonly BezierEase Pop = new(new CubicBezier(0.34, 1.3, 0.64, 1));

    private readonly AppsService _apps;
    private readonly Dictionary<string, AppButton> _buttons = [];
    private string? _hovered;
    private Point _lastPoint;
    private double _limit = double.PositiveInfinity;
    private StripFit _fit = new(0, 160, 160, false);
    private LeftIcons? _hidden;
    private readonly SlotAnimation _slots = new();

    public LeftSide(AppsService apps)
    {
        _apps = apps;
        InitializeComponent();
        apps.Changed += Sync;
        Loaded += (_, _) => Sync();
    }

    /// <summary>
    /// Settings → Left side hides icons (F-BAR-12) and the running apps (F-LEFT-07) the way <see cref="SlotAnimation"/>
    /// describes. The divider shows only between an icon and the apps. With Windows animations off, icons just appear
    /// and go.
    /// </summary>
    public void ApplyHidden(LeftIcons hidden, bool animate)
    {
        if (hidden == _hidden) return;
        animate &= _hidden is not null && SystemParameters.ClientAreaAnimation;
        _hidden = hidden;
        _slots.Show(StartButton, !hidden.HasFlag(LeftIcons.Start), animate);
        foreach (var icon in Launcher.Children.OfType<FrameworkElement>())
            _slots.Show(icon, !hidden.HasFlag(Enum.Parse<LeftIcons>((string)icon.Tag)), animate);
        var apps = !hidden.HasFlag(LeftIcons.Apps);
        _apps.ShowStrip(apps);
        _slots.Show(Strip, apps, animate);
        Rule.Visibility = apps && (hidden | LeftIcons.Apps) != EveryIcon ? Visibility.Visible : Visibility.Collapsed;
        Fit();
    }

    private bool ShowsApps => _hidden?.HasFlag(LeftIcons.Apps) != true;

    private static readonly LeftIcons EveryIcon = Enum.GetValues<LeftIcons>().Aggregate((all, icon) => all | icon);

    /// <summary>The Shelf's file count beside its icon and in its tooltip (PRD 6.2); a new drop bounces the tray.</summary>
    /// <summary>
    /// The Claude icon (F-LEFT-05, mockup renderClLauncher): a yellow dot pops in while a session waits, a green one while a
    /// finish is unseen and none waits, the icon breathes while one works (at 20 fps, PRD 8.1), and the tooltip counts them.
    /// </summary>
    public void ShowClaude(bool waiting, bool working, bool unseen, string tooltip)
    {
        ClaudeButton.ToolTip = tooltip;
        var dot = waiting ? ClaudeDotColor.Waiting : unseen ? ClaudeDotColor.Unseen : ClaudeDotColor.None;
        if (dot != ClaudeDotColor.None) ClaudeDot.Fill = dot == ClaudeDotColor.Waiting ? WaitingDot : UnseenDot;
        if (_claudeDot != dot)
        {
            var shown = _claudeDot != ClaudeDotColor.None;
            _claudeDot = dot;
            if (shown != (dot != ClaudeDotColor.None))
            {
                var pop = new DoubleAnimation(dot != ClaudeDotColor.None ? 1 : 0, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 220 : 0))
                {
                    EasingFunction = Pop,
                };
                ClaudeDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                ClaudeDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            }
        }
        var pulse = working && !waiting && SystemParameters.ClientAreaAnimation;
        if (_claudePulse == pulse) return;
        _claudePulse = pulse;
        if (!pulse)
        {
            ClaudeIcon.BeginAnimation(OpacityProperty, null);
            return;
        }
        var breathe = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(800))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Timeline.SetDesiredFrameRate(breathe, 20);
        ClaudeIcon.BeginAnimation(OpacityProperty, breathe);
    }

    private enum ClaudeDotColor { None, Waiting, Unseen }

    private static readonly SolidColorBrush WaitingDot = FrozenBrush(Color.FromRgb(0xFF, 0xD6, 0x0A));
    private static readonly SolidColorBrush UnseenDot = FrozenBrush(Color.FromRgb(0x30, 0xD1, 0x58));

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private ClaudeDotColor _claudeDot;
    private bool _claudePulse;

    public void ShowShelf(int files, bool bounce)
    {
        ShelfCount.Text = files.ToString(CultureInfo.InvariantCulture);
        ShelfCount.Visibility = files > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShelfButton.ToolTip = ShelfText.BarTooltip(files);
        if (!bounce || !SystemParameters.ClientAreaAnimation) return;
        var scale = ShelfIcon.RenderTransform as ScaleTransform ?? (ScaleTransform)(ShelfIcon.RenderTransform = new ScaleTransform());
        // Scale 1.25 and back over 380 ms on the pop curve (mockup addFiles).
        var grow = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(380) };
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(1.25, KeyTime.FromPercent(0.5), Pop));
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), Pop));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>A launcher, Shelf or Claude icon asks for its page; the island toggles it.</summary>
    public event Action<IslandPage>? PageRequested;

    /// <summary>Elements judged one by one when the floating island covers part of the bar (PRD 6.3).</summary>
    public IEnumerable<FrameworkElement> Coverable =>
        [StartButton, .. Launcher.Children.OfType<FrameworkElement>(), .. _buttons.Values.Select(b => b.Root), More];

    /// <summary>The widest the left side may get, from the bar's edge up to 24 px before the island (PRD 6.1).</summary>
    public void SetLimit(double width)
    {
        if (Math.Abs(width - _limit) < 0.5) return;
        _limit = width;
        Sync();
    }

    private void OnStart(object sender, RoutedEventArgs e) => StartMenu.Toggle();

    private void OnLaunch(object sender, RoutedEventArgs e) =>
        PageRequested?.Invoke(Enum.Parse<IslandPage>((string)((Button)sender).Tag));

    /// <summary>Adds apps that opened (popping in at the end), removes the closed ones, and lays the strip out again.</summary>
    private void Sync()
    {
        var apps = _apps.Apps;
        foreach (var gone in _buttons.Keys.Where(k => apps.All(a => a.Key != k)).ToList())
        {
            var button = _buttons[gone];
            _buttons.Remove(gone);
            button.Leave(() => Strip.Children.Remove(button.Root));
        }
        foreach (var app in apps)
        {
            if (_buttons.ContainsKey(app.Key)) continue;
            var button = new AppButton(app, () => _apps.Activate(app.Key));
            _buttons[app.Key] = button;
            Strip.Children.Add(button.Root);
            if (IsLoaded) button.Arrive();
        }
        Fit();
    }

    /// <summary>What is left after Start, the launcher and the divider decides how many icons fit (mockup <c>fitApps()</c>).</summary>
    private void Fit()
    {
        var apps = _apps.Apps;
        var used = StartButton.ActualWidth + Gap + Launcher.ActualWidth + Gap + Divider + Gap;
        var activeIndex = apps.ToList().FindIndex(a => a.Key == _apps.Active);
        var activeName = activeIndex >= 0 ? _buttons[apps[activeIndex].Key].NameWidth : 0;
        _fit = AppStrip.Fit(_limit - used, apps.Count, activeIndex, activeName);
        var hidden = new List<AppItem>();
        var toHide = _fit.Hidden;
        for (var i = apps.Count - 1; i >= 0; i--)
        {
            var hide = toHide > 0 && i != activeIndex;
            if (hide)
            {
                toHide--;
                hidden.Insert(0, apps[i]);
            }
            _buttons[apps[i].Key].Root.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        More.Visibility = hidden.Count > 0 && ShowsApps ? Visibility.Visible : Visibility.Collapsed;
        More.Content = "+" + hidden.Count.ToString(CultureInfo.InvariantCulture);
        More.ToolTip = hidden.Count > 0 ? string.Join(", ", hidden.Select(a => a.Name)) : null;
        More.Tag = hidden;
        foreach (var (key, button) in _buttons)
        {
            var active = key == _apps.Active;
            var hovered = key == _hovered && !active;
            button.Show(active, hovered && !_fit.Tight, hovered, active ? _fit.ActiveNameMax : _fit.HoverNameMax, IsLoaded);
            button.Root.ToolTip = hovered && _fit.Tight ? button.Name : null;
        }
    }

    /// <summary>Only a pointer that really moves picks the hovered app, so icons sliding under a resting pointer don't
    /// set off a chain of openings (PRD 6.2).</summary>
    private void OnStripMouseMove(object sender, MouseEventArgs e)
    {
        var at = PointToScreen(e.GetPosition(this));
        if (at == _lastPoint) return;
        _lastPoint = at;
        var over = _buttons.FirstOrDefault(b => b.Value.Root.IsMouseOver).Key;
        if (over == _hovered) return;
        _hovered = over;
        Fit();
    }

    private void OnStripMouseLeave(object sender, MouseEventArgs e)
    {
        _lastPoint = default;
        if (_hovered is null) return;
        _hovered = null;
        Fit();
    }

    /// <summary>"+N" lists the apps that didn't fit; picking one brings it to the front and onto the strip.</summary>
    private void OnMore(object sender, RoutedEventArgs e)
    {
        MoreItems.Children.Clear();
        foreach (var app in (List<AppItem>)More.Tag)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image { Source = app.Icon, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = app.Name, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Style = (Style)FindResource("ListRow"), Content = row };
            AutomationProperties.SetName(button, app.Name);
            button.Click += (_, _) =>
            {
                MoreList.IsOpen = false;
                _apps.Activate(app.Key);
            };
            MoreItems.Children.Add(button);
        }
        MoreList.IsOpen = true;
    }

    /// <summary>One app: icon 16 px and a name that slides out beside it inside a thin capsule (mockup <c>.appi</c>).</summary>
    private sealed class AppButton
    {
        private static readonly Color Clear = Color.FromArgb(0, 0xFF, 0xFF, 0xFF);
        private static readonly Color Hover = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
        private static readonly Color ActiveColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);

        private readonly Border _name;
        private readonly TextBlock _text;
        private readonly SolidColorBrush _background = new(Clear);
        private readonly ScaleTransform _scale = new(1, 1);
        private bool? _open;
        private double _nameMax = -1;

        public AppButton(AppItem app, Action activate)
        {
            Name = app.Name;
            _text = new TextBlock { Text = app.Name, FontSize = 12, Margin = new Thickness(7, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            _name = new Border { Width = 0, Opacity = 0, ClipToBounds = true, Child = _text, VerticalAlignment = VerticalAlignment.Center };
            var icon = app.Icon is null
                ? (FrameworkElement)new IconView { Glyph = (Geometry)Application.Current.FindResource("Icon.Note"), Width = 16, Height = 16 }
                : new Image { Source = app.Icon, Width = 16, Height = 16 };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(icon);
            content.Children.Add(_name);
            Root = new Border
            {
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 2, 0),
                Background = _background,
                Opacity = 0.7,
                Child = content,
                Cursor = Cursors.Arrow,
                RenderTransform = _scale,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            AutomationProperties.SetName(Root, app.Name);
            Root.MouseLeftButtonUp += (_, _) => activate();
            NameWidth = Math.Ceiling(new FormattedText(app.Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                12, Brushes.White, 1.0).WidthIncludingTrailingWhitespace);
        }

        public Border Root { get; }

        public string Name { get; }

        public double NameWidth { get; }

        /// <summary>Active: named in a white 10% capsule; hovered: named in white 8%; otherwise the icon alone at 70%.</summary>
        public void Show(bool active, bool named, bool hovered, double nameMax, bool animate)
        {
            var open = active || named;
            animate &= SystemParameters.ClientAreaAnimation;
            var colour = active ? ActiveColor : hovered ? Hover : Clear;
            Animate(_background, SolidColorBrush.ColorProperty, new ColorAnimation(colour, TimeSpan.FromMilliseconds(220)), animate, () => _background.Color = colour);
            var opacity = active || hovered ? 1 : 0.7;
            Animate(Root, UIElement.OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(160)), animate, () => Root.Opacity = opacity);
            if (open == _open && Math.Abs(nameMax - _nameMax) < 0.5) return;
            _open = open;
            _nameMax = nameMax;
            var width = open ? 7 + Math.Min(NameWidth, Math.Max(0, nameMax)) : 0;
            _text.MaxWidth = Math.Max(0, width - 7);
            var padding = open ? new Thickness(6, 0, 9, 0) : new Thickness(5, 0, 5, 0);
            Animate(Root, Border.PaddingProperty, new ThicknessAnimation(padding, TimeSpan.FromMilliseconds(300)) { EasingFunction = BezierEase.Out }, animate, () => Root.Padding = padding);
            Animate(_name, FrameworkElement.WidthProperty, BezierEase.To(width, 300, BezierEase.Out), animate, () => _name.Width = width);
            // The active name fades in 220 ms after a 90 ms wait; a hovered one in 160 ms.
            var fade = new DoubleAnimation(open ? 1 : 0, TimeSpan.FromMilliseconds(active ? 220 : 160)) { BeginTime = TimeSpan.FromMilliseconds(active ? 90 : 0) };
            Animate(_name, UIElement.OpacityProperty, fade, animate, () => _name.Opacity = open ? 1 : 0);
        }

        /// <summary>A newly opened app pops in at the end of the strip: scale 0.6 → 1 over 320 ms with a small overshoot.</summary>
        public void Arrive()
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            var grow = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = Pop };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0.7, TimeSpan.FromMilliseconds(200)));
        }

        /// <summary>A closed app shrinks away before the others close the gap.</summary>
        public void Leave(Action removed)
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                removed();
                return;
            }
            var shrink = new DoubleAnimation(0.6, TimeSpan.FromMilliseconds(160));
            shrink.Completed += (_, _) => removed();
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.6, TimeSpan.FromMilliseconds(160)));
            Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
        }

        private static void Animate(IAnimatable target, DependencyProperty property, AnimationTimeline animation, bool animate, Action set)
        {
            if (animate)
            {
                target.BeginAnimation(property, animation);
                return;
            }
            target.BeginAnimation(property, null);
            set();
        }
    }
}
