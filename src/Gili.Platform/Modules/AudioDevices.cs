using System.Runtime.InteropServices;

namespace Gili.Platform.Modules;

/// <summary>A speaker the Volume island can switch to; <see cref="Headphones"/> picks the headphones icon.</summary>
public sealed record AudioOutput(string Id, string Name, bool IsDefault, bool Headphones);

/// <summary>
/// The audio endpoints behind the Volume and Bluetooth islands (F-RIGHT-10), read only while those islands ask:
/// the active speakers and the default one, switching the default through <c>IPolicyConfig</c> (undocumented, the way
/// Store volume apps do it, PRD 9.2), and connecting a paired Bluetooth audio device through the one-shot reconnect
/// property of its kernel-streaming filter (WDK <c>KSPROPERTY_ONESHOT_RECONNECT</c>), matched by container ID.
/// </summary>
public static class AudioDevices
{
    private const int Render = 0;
    private const int Capture = 1;
    private const int StateActive = 1;
    private const int StateAll = 0xF;
    private const int ClsCtxAll = 23;
    private const ushort VtLpwstr = 31;
    private const ushort VtUi4 = 19;
    private const ushort VtClsid = 72;
    private const int FormHeadphones = 3;
    private const int FormHeadset = 5;
    private const int OneShotReconnect = 0;
    private const int OneShotDisconnect = 1;
    private const int PropertyGet = 1;

    private static readonly Guid EnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid PolicyConfigClass = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");
    private static readonly Guid BtAudioProperties = new("7fa06c40-b8f6-4c7e-8556-e8c33a12e54d");
    private static readonly PropertyKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    private static readonly PropertyKey ContainerId = new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);
    private static readonly PropertyKey FormFactor = new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);

    /// <summary>The active speakers, the default one marked; empty when Core Audio is unavailable.</summary>
    public static IReadOnlyList<AudioOutput> Outputs()
    {
        var outputs = new List<AudioOutput>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)Create(EnumeratorClass);
            string? defaultId = null;
            if (enumerator.GetDefaultAudioEndpoint(Render, 1, out var device) == 0 && device is not null) device.GetId(out defaultId);
            foreach (var endpoint in Endpoints(enumerator, Render, StateActive))
            {
                if (endpoint.GetId(out var id) != 0 || endpoint.OpenPropertyStore(0, out var store) != 0) continue;
                var name = Text(store, FriendlyName) ?? id;
                var form = Number(store, FormFactor);
                outputs.Add(new AudioOutput(id, name, id == defaultId, form is FormHeadphones or FormHeadset));
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            // No audio service: the island shows "Not available".
        }
        return outputs;
    }

    /// <summary>Makes a speaker the default for every role, as the Windows sound flyout does.</summary>
    public static bool SetDefault(string id)
    {
        try
        {
            var policy = (IPolicyConfig)Create(PolicyConfigClass);
            for (var role = 0; role < 3; role++)
            {
                if (policy.SetDefaultEndpoint(id, role) != 0) return false;
            }
            return true;
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            return false;
        }
    }

    /// <summary>The container IDs of paired Bluetooth devices that have audio endpoints, connected or not.</summary>
    public static IReadOnlySet<Guid> BluetoothAudio()
    {
        var containers = new HashSet<Guid>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)Create(EnumeratorClass);
            foreach (var (container, _) in BluetoothFilters(enumerator)) containers.Add(container);
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
        }
        return containers;
    }

    /// <summary>
    /// Asks a paired Bluetooth audio device to connect or disconnect. The call only starts it: whether the device came
    /// is seen when Windows reports it connected.
    /// </summary>
    public static bool ConnectBluetooth(Guid container, bool connect)
    {
        var asked = false;
        try
        {
            var enumerator = (IMMDeviceEnumerator)Create(EnumeratorClass);
            foreach (var (found, filter) in BluetoothFilters(enumerator))
            {
                if (found != container) continue;
                var property = new KsProperty(BtAudioProperties, connect ? OneShotReconnect : OneShotDisconnect, PropertyGet);
                if (filter.KsProperty(ref property, Marshal.SizeOf<KsProperty>(), 0, 0, out _) == 0) asked = true;
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
        }
        return asked;
    }

    /// <summary>Every endpoint's container with the kernel-streaming filter it plugs into, for Bluetooth filters only.</summary>
    private static IEnumerable<(Guid Container, IKsControl Filter)> BluetoothFilters(IMMDeviceEnumerator enumerator)
    {
        foreach (var flow in new[] { Render, Capture })
        {
            foreach (var endpoint in Endpoints(enumerator, flow, StateAll))
            {
                if (endpoint.OpenPropertyStore(0, out var store) != 0 || GuidValue(store, ContainerId) is not { } container) continue;
                var filter = Filter(enumerator, endpoint);
                if (filter is not null) yield return (container, filter);
            }
        }
    }

    private static IKsControl? Filter(IMMDeviceEnumerator enumerator, IMMDevice endpoint)
    {
        try
        {
            var topologyId = typeof(IDeviceTopology).GUID;
            if (endpoint.Activate(ref topologyId, ClsCtxAll, 0, out var topology) != 0) return null;
            if (((IDeviceTopology)topology).GetConnector(0, out var connector) != 0) return null;
            if (connector.GetDeviceIdConnectedTo(out var filterId) != 0) return null;
            // Bluetooth audio filters live under the Bluetooth enumerators (A2DP and hands-free).
            if (!filterId.Contains("bthenum", StringComparison.OrdinalIgnoreCase)
                && !filterId.Contains("bthhfenum", StringComparison.OrdinalIgnoreCase)) return null;
            if (enumerator.GetDevice(filterId, out var filter) != 0) return null;
            var ksId = typeof(IKsControl).GUID;
            return filter.Activate(ref ksId, ClsCtxAll, 0, out var ks) == 0 ? (IKsControl)ks : null;
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            return null;
        }
    }

    private static IEnumerable<IMMDevice> Endpoints(IMMDeviceEnumerator enumerator, int flow, int states)
    {
        if (enumerator.EnumAudioEndpoints(flow, states, out var collection) != 0) yield break;
        collection.GetCount(out var count);
        for (uint i = 0; i < count; i++)
        {
            if (collection.Item(i, out var device) == 0) yield return device;
        }
    }

    private static string? Text(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out var value) != 0) return null;
        try
        {
            return value.Type == VtLpwstr ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static int? Number(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out var value) != 0) return null;
        try
        {
            return value.Type == VtUi4 ? (int)value.Pointer : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static Guid? GuidValue(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out var value) != 0) return null;
        try
        {
            return value.Type == VtClsid && value.Pointer != 0 ? Marshal.PtrToStructure<Guid>(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    /// <summary>
    /// Creates a COM object by class ID. A second <c>[ComImport]</c> class for the device enumerator (AudioMonitor has
    /// one) would make the runtime hand back the other class's wrapper, and the cast to this one would fail.
    /// </summary>
    private static object Create(Guid clsid) =>
        Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, throwOnError: true)!)!;

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid format, uint id)
    {
        public readonly Guid Format = format;
        public readonly uint Id = id;
    }

    /// <summary>PROPVARIANT: the type, then the value or a pointer to it 8 bytes in.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public nint Pointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KsProperty(Guid set, int id, int flags)
    {
        public Guid Set = set;
        public int Id = id;
        public int Flags = flags;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceTopology
    {
        [PreserveSig] int GetConnectorCount(out uint count);
        [PreserveSig] int GetConnector(uint index, out IConnector connector);
    }

    [ComImport, Guid("9c2c4058-23f5-41de-877a-df3af236a09e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IConnector
    {
        [PreserveSig] int GetType(out int type);
        [PreserveSig] int GetDataFlow(out int flow);
        [PreserveSig] int ConnectTo(nint other);
        [PreserveSig] int Disconnect();
        [PreserveSig] int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool connected);
        [PreserveSig] int GetConnectedTo(out nint other);
        [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IKsControl
    {
        [PreserveSig] int KsProperty(ref KsProperty property, int length, nint data, int dataLength, out int returned);
    }

    /// <summary>IPolicyConfig (Windows 7 and later): only SetDefaultEndpoint is called; the slots before it keep the vtable order.</summary>
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    }
}
