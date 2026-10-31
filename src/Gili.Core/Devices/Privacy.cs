namespace Gili.Core.Devices;

/// <summary>The apps using the camera and the microphone right now (F-ISL-14); equal when the names are.</summary>
public sealed record PrivacyUse(IReadOnlyList<string> CameraApps, IReadOnlyList<string> MicrophoneApps)
{
    public static PrivacyUse None { get; } = new([], []);

    public bool Camera => CameraApps.Count > 0;

    public bool Microphone => MicrophoneApps.Count > 0;

    /// <summary>How many dots show: camera and microphone, one each.</summary>
    public int Count => (Camera ? 1 : 0) + (Microphone ? 1 : 0);

    public bool Equals(PrivacyUse? other) =>
        other is not null && CameraApps.SequenceEqual(other.CameraApps) && MicrophoneApps.SequenceEqual(other.MicrophoneApps);

    public override int GetHashCode() => HashCode.Combine(CameraApps.Count, MicrophoneApps.Count);
}

/// <summary>The dots' tooltips (PRD 6.3 "Indikator kamera dan mikrofon").</summary>
public static class PrivacyText
{
    public static string Camera(IReadOnlyList<string> apps) => "Camera in use: " + string.Join(", ", apps);

    public static string Microphone(IReadOnlyList<string> apps) => "Microphone in use: " + string.Join(", ", apps);
}

/// <summary>
/// Reading Windows' CapabilityAccessManager ConsentStore: an app uses the device while its key has a start time and no
/// stop time. Packaged apps are keyed by package family name, others under NonPackaged by their path with # for \.
/// </summary>
public static class PrivacyNames
{
    private const int NameLimit = 40;

    public static bool IsInUse(long start, long stop) => start > 0 && stop == 0;

    public static string ExePath(string keyName) => keyName.Replace('#', '\\');

    /// <summary>The name when Windows has none to give: the family name's last part, or the exe's file name.</summary>
    public static string Fallback(string keyName, bool packaged)
    {
        string name;
        if (packaged)
        {
            var family = keyName.Split('_')[0];
            name = family[(family.LastIndexOf('.') + 1)..];
        }
        else
        {
            name = Path.GetFileNameWithoutExtension(ExePath(keyName));
        }
        name = name.Trim();
        if (name.Length == 0) return "An app";
        return name.Length > NameLimit ? name[..(NameLimit - 1)] + "…" : name;
    }
}
