using System.IO;
using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Settings;
using Gili.Data;

namespace Gili.App;

/// <summary>The live settings. Changes apply at once; writes to settings.json are coalesced 300 ms after the last change.</summary>
public sealed class SettingsHub
{
    private readonly SettingsStore _store;
    private readonly FileLog _log;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public SettingsHub(SettingsStore store, AppSettings initial, FileLog log)
    {
        _store = store;
        _log = log;
        Current = initial;
        _saveTimer.Tick += (_, _) => Flush();
    }

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var next = change(Current).Normalize();
        if (next == Current) return;
        Current = next;
        _saveTimer.Stop();
        _saveTimer.Start();
        Changed?.Invoke(next);
    }

    public void Flush()
    {
        if (!_saveTimer.IsEnabled) return;
        _saveTimer.Stop();
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write("Could not save settings.json or its keep copy", ex);
        }
    }
}
