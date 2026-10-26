using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Gili.Core.Diagnostics;

namespace Gili.App;

/// <summary>
/// PRD 11: log the exception, release the AppBar strip, restart once. A second crash within a minute stays down.
/// The toast that should say so needs the notification service from Phase 2; until then only the log records it.
/// </summary>
internal sealed class CrashHandler(FileLog log, string markerPath, Action releaseAppBar)
{
    private int _handling;

    public void Attach(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            Fail(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Fail(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    private void Fail(Exception? exception)
    {
        if (Interlocked.Exchange(ref _handling, 1) == 1) return;
        TryRun(() => log.Write("Unhandled exception", exception));
        TryRun(releaseAppBar);

        var now = DateTimeOffset.Now;
        var previous = ReadMarker();
        TryRun(() => File.WriteAllText(markerPath, now.ToString("O", CultureInfo.InvariantCulture)));
        if (CrashPolicy.ShouldRestart(previous, now) && Environment.ProcessPath is { } exe)
            TryRun(() => Process.Start(new ProcessStartInfo(exe, "--restarted") { UseShellExecute = false }));
        Environment.Exit(1);
    }

    private DateTimeOffset? ReadMarker()
    {
        try
        {
            return DateTimeOffset.Parse(File.ReadAllText(markerPath), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Already crashing: nothing better to do than keep going towards exit.
        }
    }
}
