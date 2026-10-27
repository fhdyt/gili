using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gili.App.Island;
using Gili.Core.Clipboard;
using Gili.Core.Demo;

namespace Gili.App;

/// <summary>
/// Plays the demo (F-BAR-09) through the island's own seams: fake media into MediaService, a fake charger into the
/// IslandController, and the pages it opens. Ticks every 100 ms only while it plays.
/// </summary>
public sealed class DemoService
{
    private readonly IslandController _island;
    private readonly MediaService _media;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private DemoPlayer? _player;
    private byte[]? _cover;
    private bool _timerCardPending;

    public DemoService(Dispatcher dispatcher, IslandController island, MediaService media)
    {
        _island = island;
        _media = media;
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Step();
    }

    public bool IsRunning => _player is not null;

    public event Action? Changed;

    public void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    public void Stop()
    {
        if (_player is null) return;
        _timer.Stop();
        _clock.Reset();
        _player = null;
        _media.EndDemo();
        _island.Timers.EndDemo();
        // Only the demo's own card; a real timer's card stays.
        if (_timerCardPending) _island.DismissCard("timer");
        _timerCardPending = false;
        _island.EndDemo();
        Changed?.Invoke();
    }

    private void Start()
    {
        _player = new DemoPlayer(DemoScript.Phase1(DateTimeOffset.Now));
        _clock.Restart();
        _timer.Start();
        Changed?.Invoke();
        Step();
    }

    private void Step()
    {
        if (_player is null) return;
        foreach (var action in _player.Due(_clock.Elapsed))
        {
            switch (action)
            {
                case DemoMedia { State: { } state }:
                    _media.SetDemo(state with { Cover = _cover ??= Cover() });
                    break;
                case DemoMedia:
                    _media.SetDemo(null);
                    break;
                case DemoCopy copy:
                    var content = new ClipboardContent(false, copy.Text, [], false, false, 0, 0, null, null, null);
                    if (ClipboardSnippets.For(content, _ => null) is { } snippet) _island.ShowDemoClipboard(snippet);
                    break;
                case DemoTimer timer:
                    _island.Timers.StartDemoTimer(timer.Seconds);
                    _timerCardPending = true;
                    break;
                case DemoDismiss dismiss:
                    _island.DismissCard(dismiss.Card);
                    _timerCardPending = false;
                    break;
                case DemoPower power:
                    _island.SetDemoPower(power.Power);
                    break;
                case DemoOpen open:
                    _island.Open(open.Page);
                    break;
                case DemoClose:
                    _island.Close();
                    break;
                case DemoEnd:
                    Stop();
                    return;
            }
        }
    }

    /// <summary>A purple-to-blue square as the fake song's artwork, so the Media page's glow has colours (plan 1g decision 3).</summary>
    private static byte[] Cover()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var brush = new LinearGradientBrush(Color.FromRgb(0x5E, 0x5C, 0xE6), Color.FromRgb(0x0A, 0x84, 0xFF), 45);
            dc.DrawRectangle(brush, null, new Rect(0, 0, 300, 300));
        }
        var bitmap = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
