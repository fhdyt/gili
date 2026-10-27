using System.Runtime.InteropServices;

namespace Gili.Platform.Power;

/// <summary>
/// One Windows waitable timer set to the next alarm with resume on, so a sleeping laptop wakes for it when wake timers
/// are allowed (F-TOOL-05). When they are not, the alarm rings as missed on waking.
/// </summary>
public sealed class WakeTimer : IDisposable
{
    private const uint TIMER_ALL_ACCESS = 0x1F0003;
    private readonly IntPtr _handle = CreateWaitableTimerExW(IntPtr.Zero, null, 0, TIMER_ALL_ACCESS);

    /// <summary>Wakes the laptop at <paramref name="atUtc"/>; null cancels.</summary>
    public void Set(DateTimeOffset? atUtc)
    {
        if (_handle == IntPtr.Zero) return;
        if (atUtc is not { } at)
        {
            CancelWaitableTimer(_handle);
            return;
        }
        // A positive due time is an absolute UTC FILETIME.
        var due = at.UtcDateTime.ToFileTimeUtc();
        SetWaitableTimer(_handle, ref due, 0, IntPtr.Zero, IntPtr.Zero, true);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        CancelWaitableTimer(_handle);
        CloseHandle(_handle);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr completion, IntPtr argument,
        [MarshalAs(UnmanagedType.Bool)] bool resume);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelWaitableTimer(IntPtr timer);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
