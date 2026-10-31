using System.Runtime.InteropServices;
using Gili.Core.Devices;

namespace Gili.Platform.Devices;

/// <summary>
/// Caps Lock and Num Lock from two GetKeyState bits read 8 times a second (plan 3a): no keyboard hook, so Gili never sees
/// which keys are pressed. GetKeyState's toggle bits are global, so a background process reads them without focus.
/// </summary>
public sealed class LockKeyMonitor : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(125);
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private bool _paused;

    public LockKeyMonitor()
    {
        Current = Read();
        _timer = new Timer(_ => Poll(), null, Interval, Interval);
    }

    public LockKeys Current { get; private set; }

    /// <summary>Raised on a thread-pool thread, only when a key changed.</summary>
    public event Action<LockKeys>? Changed;

    /// <summary>Stops reading while polling is paused; a change made meanwhile is taken in quietly on resume.</summary>
    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_paused == paused) return;
            _paused = paused;
            if (!paused) Current = Read();
            _timer.Change(paused ? Timeout.InfiniteTimeSpan : Interval, paused ? Timeout.InfiniteTimeSpan : Interval);
        }
    }

    public void Dispose() => _timer.Dispose();

    private void Poll()
    {
        LockKeys now;
        lock (_gate)
        {
            if (_paused) return;
            now = Read();
            if (now == Current) return;
            Current = now;
        }
        Changed?.Invoke(now);
    }

    private static LockKeys Read() => new((GetKeyState(VkCapital) & 1) != 0, (GetKeyState(VkNumLock) & 1) != 0);

    private const int VkCapital = 0x14;
    private const int VkNumLock = 0x90;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
