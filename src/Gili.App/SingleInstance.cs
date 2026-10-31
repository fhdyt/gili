namespace Gili.App;

/// <summary>
/// F-BAR-05: a second launch tells the running instance to come forward, then exits. `Gili.exe --quit` asks it to
/// quit the normal way instead; `taskkill` is no substitute, because it also closes WPF's hidden windows and can
/// leave the process running without a bar.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Gili.SingleInstance";
    private const string SignalName = @"Local\Gili.Activate";
    private const string QuitName = @"Local\Gili.Quit";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private readonly EventWaitHandle _quit;
    private readonly RegisteredWaitHandle _registration;
    private readonly RegisteredWaitHandle _quitRegistration;

    private SingleInstance(Mutex mutex, EventWaitHandle signal, Action onActivateRequest, Action onQuitRequest)
    {
        _mutex = mutex;
        _signal = signal;
        _quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitName);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            signal, (_, _) => onActivateRequest(), null, Timeout.Infinite, executeOnlyOnce: false);
        _quitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _quit, (_, _) => onQuitRequest(), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    /// <summary>Returns false when no instance is running.</summary>
    public static bool RequestQuit()
    {
        if (!EventWaitHandle.TryOpenExisting(QuitName, out var quit)) return false;
        using (quit) quit.Set();
        return true;
    }

    /// <summary>True once no instance holds the mutex, at most <paramref name="timeout"/> later.</summary>
    public static bool WaitForExit(TimeSpan timeout)
    {
        if (!Mutex.TryOpenExisting(MutexName, out var mutex)) return true;
        using (mutex)
        {
            try
            {
                if (!mutex.WaitOne(timeout)) return false;
            }
            catch (AbandonedMutexException)
            {
                // The instance died holding it, which is gone too.
            }
            mutex.ReleaseMutex();
            return true;
        }
    }

    /// <summary>
    /// Returns null when another instance is running. <paramref name="wait"/> lets a restart after a crash wait
    /// for the dying instance to let go of the mutex instead of giving up at once.
    /// </summary>
    /// <param name="signalExisting">False for a launch at sign-in: the running copy is left alone (LaunchRules).</param>
    public static SingleInstance? Acquire(TimeSpan wait, bool signalExisting, Action onActivateRequest, Action onQuitRequest)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool owned;
        try
        {
            owned = mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            owned = true;
        }

        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        if (owned) return new SingleInstance(mutex, signal, onActivateRequest, onQuitRequest);

        if (signalExisting) signal.Set();
        signal.Dispose();
        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _quitRegistration.Unregister(null);
        _signal.Dispose();
        _quit.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
