using System.Globalization;

namespace Gili.Core.Media;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public string Hex => string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}");
}

/// <summary>
/// The track artwork's two main colours, worked out once per track: the first tints the slot's level meter, the source
/// dot and the ambient glow, the second the glow's other blob (PRD 6.3 "Artwork musik", "Ambient glow").
/// </summary>
public static class ArtworkPalette
{
    private const int MaxSamples = 32;
    private const int Buckets = 12;

    /// <summary>
    /// Colours from 32-bit BGRA pixels. Greys, near black and near white are skipped and the rest grouped into 30° hue
    /// buckets: the biggest bucket's average is primary, and the next big bucket at least 30° away (with at least 10% of
    /// the primary's pixels) is secondary, or primary again. Null when nothing colourful is left.
    /// </summary>
    public static (Rgb Primary, Rgb Secondary)? From(ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4) return null;
        var stepX = Math.Max(1, (width + MaxSamples - 1) / MaxSamples);
        var stepY = Math.Max(1, (height + MaxSamples - 1) / MaxSamples);
        var count = new int[Buckets];
        var sums = new long[Buckets, 3];
        for (var y = 0; y < height; y += stepY)
        {
            for (var x = 0; x < width; x += stepX)
            {
                var i = (y * width + x) * 4;
                byte b = bgra[i], g = bgra[i + 1], r = bgra[i + 2];
                if (Hue(r, g, b) is not { } hue) continue;
                var bucket = (int)(hue / 30) % Buckets;
                count[bucket]++;
                sums[bucket, 0] += r;
                sums[bucket, 1] += g;
                sums[bucket, 2] += b;
            }
        }
        var first = Biggest(count, exclude: -1);
        if (count[first] == 0) return null;
        var primary = Average(sums, count, first);
        var second = Biggest(count, exclude: first);
        var secondary = count[second] * 10 >= count[first] && HueDistance(first, second) >= 1
            ? Average(sums, count, second)
            : primary;
        return (primary, secondary);
    }

    /// <summary>The colour's hue in degrees, or null for a grey, near black or near white pixel.</summary>
    private static double? Hue(byte r, byte g, byte b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var value = max / 255.0;
        var saturation = max == 0 ? 0 : (max - min) / (double)max;
        if (saturation < 0.2 || value < 0.15 || (value > 0.97 && saturation < 0.1)) return null;
        double delta = max - min;
        var hue = max == r ? (g - b) / delta % 6 : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4;
        return (hue * 60 + 360) % 360;
    }

    private static int Biggest(int[] count, int exclude)
    {
        var best = exclude == 0 ? 1 : 0;
        for (var i = 0; i < count.Length; i++)
        {
            if (i != exclude && count[i] > count[best]) best = i;
        }
        return best;
    }

    /// <summary>Distance between two buckets in buckets (one bucket = 30°), around the colour wheel.</summary>
    private static int HueDistance(int a, int b)
    {
        var d = Math.Abs(a - b);
        return Math.Min(d, Buckets - d);
    }

    private static Rgb Average(long[,] sums, int[] count, int bucket)
    {
        var n = count[bucket];
        return new(
            (byte)Math.Round(sums[bucket, 0] / (double)n),
            (byte)Math.Round(sums[bucket, 1] / (double)n),
            (byte)Math.Round(sums[bucket, 2] / (double)n));
    }
}
