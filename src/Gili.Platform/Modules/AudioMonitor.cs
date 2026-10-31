using System.Runtime.InteropServices;

namespace Gili.Platform.Modules;

/// <summary>
/// The default speaker's volume and mute from Core Audio callbacks, never polled (PRD 6.4); it follows a new default
/// device when one is plugged in or chosen.
/// </summary>
public sealed class AudioMonitor : IDisposable
{
    private readonly IMMDeviceEnumerator? _enumerator;
    private readonly DeviceClient _devices;
    private readonly VolumeClient _volume;
    private IAudioEndpointVolume? _endpoint;

    public AudioMonitor()
    {
        _devices = new DeviceClient(this);
        _volume = new VolumeClient(this);
        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            _enumerator.RegisterEndpointNotificationCallback(_devices);
            Attach();
        }
        catch (COMException)
        {
            _enumerator = null;
        }
    }

    /// <summary>Raised on a Core Audio thread.</summary>
    public event Action? Changed;

    /// <summary>Volume 0–100 and mute, or null without a speaker.</summary>
    public (int Percent, bool Muted)? Current { get; private set; }

    /// <summary>Only ever on the user's click (Unmute on a card, PRD 6.6, or the Volume island); Gili never changes the volume itself.</summary>
    public void Unmute()
    {
        var context = Guid.Empty;
        _endpoint?.SetMute(false, ref context);
    }

    /// <summary>The volume slider in the Volume island (F-RIGHT-10); moving it up from 0 also unmutes, like Windows.</summary>
    public void SetVolume(int percent)
    {
        var context = Guid.Empty;
        _endpoint?.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref context);
        if (percent > 0 && Current is { Muted: true }) _endpoint?.SetMute(false, ref context);
    }

    /// <summary>The speaker button in the Volume island.</summary>
    public void SetMute(bool muted)
    {
        var context = Guid.Empty;
        _endpoint?.SetMute(muted, ref context);
    }

    public void Dispose()
    {
        Detach();
        if (_enumerator is not null) _enumerator.UnregisterEndpointNotificationCallback(_devices);
    }

    private void Attach()
    {
        Detach();
        if (_enumerator is null) return;
        if (_enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleMultimedia, out var device) != 0 || device is null)
        {
            Current = null;
            Changed?.Invoke();
            return;
        }
        var iid = typeof(IAudioEndpointVolume).GUID;
        device.Activate(ref iid, ClsCtxAll, 0, out var endpoint);
        _endpoint = (IAudioEndpointVolume)endpoint;
        _endpoint.RegisterControlChangeNotify(_volume);
        Read();
    }

    private void Detach()
    {
        if (_endpoint is null) return;
        _endpoint.UnregisterControlChangeNotify(_volume);
        _endpoint = null;
    }

    private void Read()
    {
        if (_endpoint is null) return;
        _endpoint.GetMasterVolumeLevelScalar(out var level);
        _endpoint.GetMute(out var muted);
        Current = ((int)Math.Round(level * 100), muted);
        Changed?.Invoke();
    }

    private const int DataFlowRender = 0;
    private const int RoleMultimedia = 1;
    private const int ClsCtxAll = 23;

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(nint data);
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int dataFlow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    [ComVisible(true)]
    private sealed class VolumeClient(AudioMonitor owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(nint data)
        {
            // AUDIO_VOLUME_NOTIFICATION_DATA: event context GUID, then muted (BOOL) and the master level (float).
            var muted = Marshal.ReadInt32(data, 16) != 0;
            var level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(data, 20));
            owner.Current = ((int)Math.Round(level * 100), muted);
            owner.Changed?.Invoke();
            return 0;
        }
    }

    [ComVisible(true)]
    private sealed class DeviceClient(AudioMonitor owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string id, int state) => 0;

        public int OnDeviceAdded(string id) => 0;

        public int OnDeviceRemoved(string id) => 0;

        public int OnDefaultDeviceChanged(int dataFlow, int role, string? id)
        {
            if (dataFlow == DataFlowRender && role == RoleMultimedia) owner.Attach();
            return 0;
        }

        public int OnPropertyValueChanged(string id, PropertyKey key) => 0;
    }
}
