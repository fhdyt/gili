using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using Gili.Core;
using Gili.Core.About;
using Gili.Core.Diagnostics;
using Gili.Platform.Shell;

namespace Gili.App;

/// <summary>
/// The update check (F-PUB-04, design 5d): the signed GitHub copy asks GitHub Releases, the Store copy asks the Store,
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
    private string? _installerUrl;
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
            if (_installerUrl is null)
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
        Changed?.Invoke();
    }

    public void Dispose() => _timer.Stop();
}
