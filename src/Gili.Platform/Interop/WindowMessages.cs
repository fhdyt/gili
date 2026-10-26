namespace Gili.Platform.Interop;

public static class WindowMessages
{
    public const int WM_ACTIVATE = 0x0006;
    public const int WM_TIMECHANGE = 0x001E;
    public const int WM_WINDOWPOSCHANGED = 0x0047;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_POWERBROADCAST = 0x0218;
    public const int WM_MOUSEHWHEEL = 0x020E;
    public const int WM_DPICHANGED = 0x02E0;
    public const nint PBT_APMSUSPEND = 0x0004;
    public const nint PBT_APMRESUMEAUTOMATIC = 0x0012;
}
