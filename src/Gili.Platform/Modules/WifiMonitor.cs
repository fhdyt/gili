using System.Runtime.InteropServices;
using System.Text;

namespace Gili.Platform.Modules;

/// <summary>The Wi‑Fi radio, connection, network name and signal; NameDenied when Windows withholds the name without
/// Location access (F-RIGHT-02).</summary>
public sealed record WifiState(bool RadioOn, bool Connected, string? Ssid, int Quality, bool NameDenied);

/// <summary>
/// Reads the first Wi‑Fi adapter through the Native WLAN API (PRD 9.2) and raises Changed when Windows reports a
/// connection change; the signal itself is read again by the app every 10 s.
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

    private readonly nint _handle;
    private readonly NotificationCallback _callback;

    public WifiMonitor()
    {
        _callback = (_, _) => Changed?.Invoke();
        if (WlanOpenHandle(ClientVersion, 0, out _, out _handle) != 0) _handle = 0;
        if (_handle != 0) WlanRegisterNotification(_handle, NotificationSourceAcm | NotificationSourceMsm, true, _callback, 0, 0, out _);
    }

    /// <summary>Raised on a thread-pool thread.</summary>
    public event Action? Changed;

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

    public void Dispose()
    {
        if (_handle != 0) WlanCloseHandle(_handle, 0);
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
    private static extern void WlanFreeMemory(nint memory);
}
