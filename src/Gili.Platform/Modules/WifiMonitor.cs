using System.Runtime.InteropServices;
using System.Text;
using Gili.Core.Modules;

namespace Gili.Platform.Modules;

/// <summary>The Wi‑Fi radio, connection, network name and signal; NameDenied when Windows withholds the name without
/// Location access (F-RIGHT-02).</summary>
public sealed record WifiState(bool RadioOn, bool Connected, string? Ssid, int Quality, bool NameDenied);

/// <summary>
/// Reads the first Wi‑Fi adapter through the Native WLAN API (PRD 9.2) and raises Changed when Windows reports a
/// connection change; the signal itself is read again by the app every 10 s. The Wi‑Fi island (F-RIGHT-10) reads the
/// network list, asks for a scan, and connects with a saved profile only while it is open.
/// </summary>
public sealed class WifiMonitor : IDisposable
{
    private const uint ClientVersion = 2;
    private const int OpcodeRadioState = 4;
    private const int OpcodeCurrentConnection = 7;
    private const int StateConnected = 1;
    private const int RadioOn = 1;
    private const uint ErrorAccessDenied = 5;
    private const uint NotificationSourceAcm = 0x08;
    private const uint NotificationSourceMsm = 0x10;
    private const int InterfaceInfoSize = 16 + 512 + 4;
    private const int AvailableNetworkSize = 628;
    private const int AcmScanComplete = 7;
    private const int AcmScanFail = 8;
    private const int AcmConnectionComplete = 10;
    private const int AcmConnectionAttemptFail = 11;
    private const int AcmScanListRefresh = 26;
    private const int FlagConnected = 1;
    private const int FlagHasProfile = 2;

    private readonly NotificationCallback _callback;
    private readonly object _gate = new();
    private nint _handle;
    private bool _disposed;

    /// <summary>
    /// The WLAN handle opens on a thread-pool thread: in the packaged copy Windows first asks "Let Gili access your precise
    /// location?" and the call waits for the answer, which must not hold the bar back (PRD 12). Until it opens, Wi‑Fi reads
    /// as absent; <see cref="Changed"/> fires once it is ready.
    /// </summary>
    public WifiMonitor()
    {
        _callback = (data, _) => OnNotification(data);
        Task.Run(Open);
    }

    /// <summary>Raised on a thread-pool thread.</summary>
    public event Action? Changed;

    /// <summary>A scan finished or Windows refreshed its list; raised on a thread-pool thread.</summary>
    public event Action? ListChanged;

    /// <summary>A connection Gili or anything else started finished: true when it connected.</summary>
    public event Action<bool>? ConnectFinished;

    private delegate void NotificationCallback(nint data, nint context);

    /// <summary>Null when there is no Wi‑Fi adapter.</summary>
    public WifiState? Read()
    {
        if (_handle == 0 || WlanEnumInterfaces(_handle, 0, out var list) != 0) return null;
        try
        {
            if (Marshal.ReadInt32(list) == 0) return null;
            // WLAN_INTERFACE_INFO_LIST: two DWORDs, then the entries; take the first adapter.
            var info = list + 8;
            var guid = Marshal.PtrToStructure<Guid>(info);
            var state = Marshal.ReadInt32(info, InterfaceInfoSize - 4);
            var radioOn = ReadRadio(guid);
            if (state != StateConnected) return new WifiState(radioOn, false, null, 0, false);
            var result = WlanQueryInterface(_handle, ref guid, OpcodeCurrentConnection, 0, out _, out var data, out _);
            if (result == ErrorAccessDenied) return new WifiState(radioOn, true, null, 0, true);
            if (result != 0) return new WifiState(radioOn, false, null, 0, false);
            try
            {
                // WLAN_CONNECTION_ATTRIBUTES: state, mode, 256-char profile name, then the association attributes.
                const int association = 8 + 512;
                var length = Math.Clamp(Marshal.ReadInt32(data, association), 0, 32);
                var bytes = new byte[length];
                Marshal.Copy(data + association + 4, bytes, 0, length);
                var quality = Marshal.ReadInt32(data, association + 56);
                return new WifiState(radioOn, true, Encoding.UTF8.GetString(bytes), quality, false);
            }
            finally
            {
                WlanFreeMemory(data);
            }
        }
        finally
        {
            WlanFreeMemory(list);
        }
    }

    /// <summary>Windows' current list for the first adapter, one entry per network and band; empty without an adapter.</summary>
    public IReadOnlyList<WifiNetwork> Networks()
    {
        if (Adapter() is not { } guid) return [];
        if (WlanGetAvailableNetworkList(_handle, ref guid, 0, 0, out var list) != 0) return [];
        try
        {
            // WLAN_AVAILABLE_NETWORK_LIST: count, index, then 628-byte entries: profile name (256 chars), SSID length and
            // bytes, …, signal quality at 604, security at 608, flags at 620.
            var count = Marshal.ReadInt32(list);
            var networks = new List<WifiNetwork>(count);
            for (var i = 0; i < count; i++)
            {
                var entry = list + 8 + i * AvailableNetworkSize;
                var profile = Marshal.PtrToStringUni(entry);
                var length = Math.Clamp(Marshal.ReadInt32(entry, 512), 0, 32);
                var bytes = new byte[length];
                Marshal.Copy(entry + 516, bytes, 0, length);
                var flags = Marshal.ReadInt32(entry, 620);
                networks.Add(new WifiNetwork(
                    Encoding.UTF8.GetString(bytes),
                    Marshal.ReadInt32(entry, 604),
                    Marshal.ReadInt32(entry, 608) != 0,
                    (flags & FlagHasProfile) != 0,
                    (flags & FlagConnected) != 0,
                    string.IsNullOrEmpty(profile) ? null : profile));
            }
            return networks;
        }
        finally
        {
            WlanFreeMemory(list);
        }
    }

    /// <summary>Asks Windows for a fresh scan; <see cref="ListChanged"/> follows within a few seconds.</summary>
    public void Scan()
    {
        if (Adapter() is { } guid) WlanScan(_handle, ref guid, 0, 0, 0);
    }

    /// <summary>Connects with a saved profile; false when the request itself failed.</summary>
    public bool Connect(string profile)
    {
        if (Adapter() is not { } guid) return false;
        var name = Marshal.StringToHGlobalUni(profile);
        try
        {
            var parameters = new ConnectionParameters { Mode = 0, Profile = name, BssType = 1 };
            return WlanConnect(_handle, ref guid, ref parameters, 0) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(name);
        }
    }

    private void Open()
    {
        if (WlanOpenHandle(ClientVersion, 0, out _, out var handle) != 0) return;
        WlanRegisterNotification(handle, NotificationSourceAcm | NotificationSourceMsm, true, _callback, 0, 0, out _);
        lock (_gate)
        {
            if (_disposed)
            {
                WlanCloseHandle(handle, 0);
                return;
            }
            _handle = handle;
        }
        Changed?.Invoke();
    }

    public void Dispose()
    {
        nint handle;
        lock (_gate)
        {
            _disposed = true;
            handle = _handle;
            _handle = 0;
        }
        if (handle != 0) WlanCloseHandle(handle, 0);
    }

    private Guid? Adapter()
    {
        if (_handle == 0 || WlanEnumInterfaces(_handle, 0, out var list) != 0) return null;
        try
        {
            return Marshal.ReadInt32(list) == 0 ? null : Marshal.PtrToStructure<Guid>(list + 8);
        }
        finally
        {
            WlanFreeMemory(list);
        }
    }

    private void OnNotification(nint data)
    {
        // WLAN_NOTIFICATION_DATA: source, code, adapter GUID, data size, then the data pointer at 32.
        if (data != 0 && Marshal.ReadInt32(data) == (int)NotificationSourceAcm)
        {
            switch (Marshal.ReadInt32(data, 4))
            {
                case AcmScanComplete or AcmScanFail or AcmScanListRefresh:
                    ListChanged?.Invoke();
                    break;
                case AcmConnectionComplete:
                    // WLAN_CONNECTION_NOTIFICATION_DATA: the reason code sits after mode, profile, SSID, type and security.
                    var details = Marshal.ReadIntPtr(data, 32);
                    ConnectFinished?.Invoke(details != 0 && Marshal.ReadInt32(details, 560) == 0);
                    break;
                case AcmConnectionAttemptFail:
                    ConnectFinished?.Invoke(false);
                    break;
            }
        }
        Changed?.Invoke();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConnectionParameters
    {
        public int Mode;
        public nint Profile;
        public nint Ssid;
        public nint BssidList;
        public int BssType;
        public int Flags;
    }

    private bool ReadRadio(Guid guid)
    {
        if (WlanQueryInterface(_handle, ref guid, OpcodeRadioState, 0, out _, out var data, out _) != 0) return true;
        try
        {
            // WLAN_RADIO_STATE: count, then per PHY: index, software state, hardware state. On only if both are on.
            if (Marshal.ReadInt32(data) == 0) return true;
            return Marshal.ReadInt32(data, 8) == RadioOn && Marshal.ReadInt32(data, 12) == RadioOn;
        }
        finally
        {
            WlanFreeMemory(data);
        }
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint clientVersion, nint reserved, out uint negotiatedVersion, out nint handle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(nint handle, nint reserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(nint handle, nint reserved, out nint list);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(nint handle, ref Guid guid, int opcode, nint reserved, out uint size, out nint data, out int valueType);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanRegisterNotification(nint handle, uint sources, [MarshalAs(UnmanagedType.Bool)] bool ignoreDuplicate,
        NotificationCallback callback, nint context, nint reserved, out uint previous);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(nint handle, ref Guid guid, uint flags, nint reserved, out nint list);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(nint handle, ref Guid guid, nint ssid, nint ie, nint reserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanConnect(nint handle, ref Guid guid, ref ConnectionParameters parameters, nint reserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(nint memory);
}
