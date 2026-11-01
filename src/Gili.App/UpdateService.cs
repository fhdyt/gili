using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using Gili.Core;
using Gili.Core.About;
using Gili.Core.Diagnostics;
using Gili.Platform.Shell;

namespace Gili.App;

/// <summary>
/// The update check (F-PUB-04, design 5d): the GitHub copy asks GitHub Releases (unsigned, it only tells and opens the
/// release page; revision 15), the Store copy asks the Store,
/// 60 seconds after start and then once a day. Failing is quiet (PRD 11); Install update runs only an installer signed
/// by the same publisher as this Gili.exe.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly Version Mine = typeof(UpdateService).Assembly.GetName().Version!;

    private readonly SettingsHub _settings;
    private readonly FileLog _log;
    private readonly InstallKind _kind;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(60) };

    /// <summary>While a GitHub update waits, once a minute: is now a good moment to install it by itself (issue #27)?</summary>
    private readonly DispatcherTimer _autoTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private string? _installerUrl;
    private Version? _autoTried;
    private bool _checking;

    public UpdateService(SettingsHub settings, FileLog log, InstallKind kind)
    {
        _settings = settings;
        _log = log;
        _kind = kind;
        State = UpdateRules.Initial(kind, signed: false);
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(1);
            await CheckAsync();
        };
        _autoTimer.Tick += (_, _) => InstallIfAway();
    }

    /// <summary>How the computer is being used right now, for Install updates automatically; set by the app.</summary>
    public Func<UpdateMoment>? Moment { get; set; }

    /// <summary>
    /// Install updates automatically (F-PUB-04, revision 14): the GitHub copy installs a waiting update once nobody is
    /// using the computer. The Store keeps its own copy up to date, so it never does this.
    /// </summary>
    private void InstallIfAway()
    {
        if (Moment is null || !UpdateRules.ShouldInstallNow(_settings.Current.AutoUpdate, State, Moment())) return;
        // One try per version: a release without its installer, a failed download or an installer that wouldn't start
        // waits for the next day's check (or Install update) instead of downloading again every minute all night.
        if (State.Latest == _autoTried) return;
        _autoTried = State.Latest;
        _autoTimer.Stop();
        _log.Write("Installing the update by itself while the computer is not in use");
        _ = InstallAsync(0, _ => { });
    }

    public UpdateState State { get; private set; }

    public event Action? Changed;

    /// <summary>Whether this copy checks at all: Gili.exe's signature is read off the UI thread first.</summary>
    public async Task StartAsync()
    {
        var path = Environment.ProcessPath;
        var signed = _kind == InstallKind.Installed && path is not null && await Task.Run(() => Signatures.IsSigned(path));
        Set(UpdateRules.Initial(_kind, signed));
        if (State.Status != UpdateStatus.Off) _timer.Start();
    }

    /// <summary>Asks once the last answer is a day old; offline keeps the last state.</summary>
    private async Task CheckAsync()
    {
        if (_checking || State.Status is UpdateStatus.Off or UpdateStatus.Installing) return;
        if (!UpdateRules.IsDue(_settings.Current.LastUpdateCheck, DateTimeOffset.Now)) return;
        _checking = true;
        try
        {
            if (_kind == InstallKind.Store)
            {
                var waiting = await StoreUpdates.WaitingAsync();
                Answered(waiting is { } version ? UpdateRules.Checked(Mine, version, null) : UpdateRules.Checked(Mine, Mine, null));
            }
            else if (await GithubReleases.LatestAsync(CancellationToken.None) is { } release)
            {
                _installerUrl = release.InstallerUrl;
                Answered(UpdateRules.Checked(Mine, release.Version, release.PageUrl));
            }
            else Failed(null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Runtime.InteropServices.COMException)
        {
            Failed(ex);
        }
        finally
        {
            _checking = false;
        }
    }

    private void Answered(UpdateState state)
    {
        // A day later, a version whose automatic install failed may be tried once more.
        _autoTried = null;
        state = state with { NotifyOnly = State.NotifyOnly };
        _settings.Update(s => s with { LastUpdateCheck = DateTimeOffset.Now });
        if (state.Status == UpdateStatus.Available && State.Status != UpdateStatus.Available)
            _log.Write("Update available: " + AboutInfo.Format(state.Latest!));
        Set(state);
    }

    private void Failed(Exception? ex)
    {
        _log.Write("Could not check for updates", ex);
        Set(UpdateRules.Failed(State, _settings.Current.LastUpdateCheck, DateTimeOffset.Now));
    }

    /// <summary>Install update; <paramref name="owner"/> parents the Store's dialog. A release without its installer opens its page.</summary>
    public async Task InstallAsync(nint owner, Action<string> openPage)
    {
        if (State.Status != UpdateStatus.Available) return;
        var available = State;
        Set(available with { Status = UpdateStatus.Installing });
        try
        {
            if (_kind == InstallKind.Store)
            {
                if (!await StoreUpdates.InstallAsync(owner)) Set(available);
                return;
            }
            // Unsigned, Gili can't check whose installer it would run, so Download opens the release page (revision 15).
            if (_installerUrl is null || available.NotifyOnly)
            {
                if (available.PageUrl is { } page) openPage(page);
                Set(available);
                return;
            }
            var path = await GithubReleases.DownloadAsync(_installerUrl, available.Latest!, CancellationToken.None);
            if (!await Task.Run(() => Signatures.SameSigner(path, Environment.ProcessPath!)))
            {
                File.Delete(path);
                _log.Write("The downloaded installer is not signed by Gili's publisher, so it was deleted");
                Set(available with { Status = UpdateStatus.NotVerified });
                return;
            }
            _log.Write("Installing update " + AboutInfo.Format(available.Latest!));
            // The installer quits Gili through --quit and starts the new version.
            if (!GithubReleases.RunInstaller(path)) Set(available);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException
            or System.Runtime.InteropServices.COMException)
        {
            _log.Write("Could not install the update", ex);
            Set(available);
        }
    }

    private void Set(UpdateState state)
    {
        if (state == State) return;
        State = state;
        if (_kind == InstallKind.Installed && state.Status == UpdateStatus.Available && !state.NotifyOnly) _autoTimer.Start();
        else _autoTimer.Stop();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _timer.Stop();
        _autoTimer.Stop();
    }
}
