using System.Globalization;

namespace Gili.Core.Settings;

/// <summary>
/// The bar's colour (F-BAR-14, revision 14, issue #35): eight dark presets that keep the bar's white text readable, or
/// any <c>#RRGGBB</c> code. A light code is allowed but its row warns, since the text and icons stay white.
/// </summary>
public static class BarColors
{
    public const string Default = "#161618";
    public const string LightNote = "Light colors make the bar's text hard to read";
    public const string BadCode = "Use a code like #1E2A44";

    public static IReadOnlyList<(string Name, string Hex)> Presets { get; } =
    [
        ("Graphite", Default), ("Black", "#000000"), ("Slate", "#1C2530"), ("Navy", "#15203D"),
        ("Forest", "#13281C"), ("Plum", "#2A1733"), ("Wine", "#331820"), ("Cocoa", "#2B2018"),
    ];

    /// <summary>"#1e2a44", "1E2A44" or "#abc" as "#1E2A44"; null when it is not a colour code.</summary>
    public static string? Parse(string? text)
    {
        var code = (text ?? "").Trim().TrimStart('#');
        if (code.Length == 3) code = string.Concat(code.Select(c => new string(c, 2)));
        // Hex digits only: HexNumber alone would let "# 12345" through with its space.
        if (code.Length != 6 || !code.All(Uri.IsHexDigit)) return null;
        return "#" + code.ToUpperInvariant();
    }

    public static (byte R, byte G, byte B) Rgb(string hex)
    {
        var value = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>The preset's name, or null for a custom colour.</summary>
    public static string? NameOf(string hex) => Presets.FirstOrDefault(p => p.Hex == hex).Name;

    /// <summary>White on this colour reads below 4.5:1 (WCAG AA for the bar's small text).</summary>
    public static bool IsLight(string hex) => 1.05 / (Luminance(hex) + 0.05) < 4.5;

    private static double Luminance(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    private static double Linear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
