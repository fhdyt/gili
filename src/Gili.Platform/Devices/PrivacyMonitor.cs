using System.Diagnostics;
using System.Runtime.InteropServices;
using Gili.Core.Devices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Management.Deployment;

namespace Gili.Platform.Devices;

/// <summary>
/// The apps using the camera and the microphone, from the CapabilityAccessManager ConsentStore that Windows' own
/// microphone indicator reads (plan 3a). One background thread waits for the registry to change; nothing is polled.
/// </summary>
public sealed class PrivacyMonitor : IDisposable
{
    private const string Store = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";
    private const string NonPackaged = "NonPackaged";

    private readonly ManualResetEvent _stop = new(false);
    private readonly Dictionary<string, string> _names = [];
    private readonly string? _ownExe = Environment.ProcessPath;
    private readonly string? _ownFamily = OwnFamily();
    private Thread? _thread;

    public PrivacyUse Current { get; private set; } = PrivacyUse.None;

    /// <summary>Raised on the monitor's thread when the apps using either device change.</summary>
    public event Action<PrivacyUse>? Changed;

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "Gili privacy" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(1));
        _stop.Dispose();
    }

    private void Run()
    {
        using var camera = Registry.CurrentUser.OpenSubKey(Store + "webcam");
        using var microphone = Registry.CurrentUser.OpenSubKey(Store + "microphone");
        using var cameraChanged = new AutoResetEvent(false);
        using var microphoneChanged = new AutoResetEvent(false);
        Publish(camera, microphone);
        while (true)
        {
            // A notification fires once; it is asked for again before the keys are read, so no change slips between.
            Watch(camera, cameraChanged);
            Watch(microphone, microphoneChanged);
            if (WaitHandle.WaitAny([_stop, cameraChanged, microphoneChanged]) == 0) return;
            Publish(camera, microphone);
        }
    }

    private void Publish(RegistryKey? camera, RegistryKey? microphone)
    {
        var use = new PrivacyUse(InUse(camera), InUse(microphone));
        if (use == Current) return;
        Current = use;
        Changed?.Invoke(use);
    }

    private static void Watch(RegistryKey? key, AutoResetEvent changed)
    {
        if (key is null) return;
        RegNotifyChangeKeyValue(key.Handle, watchSubtree: true, NotifyName | NotifyLastSet, changed.SafeWaitHandle, asynchronous: true);
    }

    private List<string> InUse(RegistryKey? store)
    {
        var apps = new List<string>();
        if (store is null) return apps;
        try
        {
            foreach (var name in store.GetSubKeyNames())
            {
                if (name == NonPackaged)
                {
                    using var exes = store.OpenSubKey(NonPackaged);
                    foreach (var exe in exes?.GetSubKeyNames() ?? []) Add(exes!, exe, packaged: false, apps);
                }
                else
                {
                    Add(store, name, packaged: true, apps);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A key removed while it was read: the next notification reads it again.
        }
        return [.. apps.Distinct()];
    }

    private void Add(RegistryKey parent, string name, bool packaged, List<string> apps)
    {
        using var key = parent.OpenSubKey(name);
        if (key is null || !PrivacyNames.IsInUse(Number(key, "LastUsedTimeStart"), Number(key, "LastUsedTimeStop"))) return;
        if (packaged ? name.Equals(_ownFamily, StringComparison.OrdinalIgnoreCase)
                     : string.Equals(PrivacyNames.ExePath(name), _ownExe, StringComparison.OrdinalIgnoreCase)) return;
        apps.Add(Name(name, packaged));
    }

    private static long Number(RegistryKey key, string value) => key.GetValue(value) is long n ? n : 0;

    private string Name(string key, bool packaged)
    {
        if (_names.TryGetValue(key, out var known)) return known;
        string? name = null;
        try
        {
            name = packaged
                ? new PackageManager().FindPackagesForUser("", key).FirstOrDefault()?.DisplayName
                : FileVersionInfo.GetVersionInfo(PrivacyNames.ExePath(key)).FileDescription;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // An uninstalled app or a file Gili cannot read: the name comes from the key itself.
        }
        name = string.IsNullOrWhiteSpace(name) ? PrivacyNames.Fallback(key, packaged) : name.Trim();
        _names[key] = name;
        return name;
    }

    private static string? OwnFamily() => Shell.PackageInfo.IsPackaged ? Windows.ApplicationModel.Package.Current.Id.FamilyName : null;

    private const int NotifyName = 0x1;
    private const int NotifyLastSet = 0x4;

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key, bool watchSubtree, int filter, SafeWaitHandle evt, bool asynchronous);
}
