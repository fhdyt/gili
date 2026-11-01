using System.Runtime.InteropServices;
using Gili.Core.Settings;

namespace Gili.Platform.Shell;

/// <summary>
/// Glass behind the bar (F-BAR-06): Off draws only the WPF tint, Light is blur-behind, Strong is acrylic.
/// Uses the undocumented accent policy because DWM system backdrops turn solid while the window is inactive,
/// and the bar never activates. WPF paints the tint on top, so the accent's own gradient stays nearly clear.
/// </summary>
public static class BarMaterial
{
    private const int WCA_ACCENT_POLICY = 19;
    private const int ACCENT_DISABLED = 0;
    private const int ACCENT_ENABLE_BLURBEHIND = 3;
    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <param name="color">The bar's tint as #RRGGBB (F-BAR-14); acrylic gets it at alpha 1 as its gradient.</param>
    public static void Apply(IntPtr hwnd, GlassLevel glass, string color = BarColors.Default)
    {
        var (r, g, b) = BarColors.Rgb(color);
        var policy = new AccentPolicy
        {
            AccentState = glass switch
            {
                GlassLevel.Light => ACCENT_ENABLE_BLURBEHIND,
                GlassLevel.Strong => ACCENT_ENABLE_ACRYLICBLURBEHIND,
                _ => ACCENT_DISABLED,
            },
            // AABBGGRR: the bar's tint at alpha 1, so acrylic has a gradient to work with.
            GradientColor = glass == GlassLevel.Strong ? 0x01000000u | ((uint)b << 16) | ((uint)g << 8) | r : 0u,
        };
        var size = Marshal.SizeOf<AccentPolicy>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, buffer, false);
            var data = new WindowCompositionAttributeData { Attribute = WCA_ACCENT_POLICY, Data = buffer, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
