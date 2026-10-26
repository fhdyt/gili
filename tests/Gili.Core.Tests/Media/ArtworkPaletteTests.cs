using Gili.Core.Media;

namespace Gili.Core.Tests.Media;

public class ArtworkPaletteTests
{
    [Fact]
    public void A_single_colour_is_both_colours()
    {
        var palette = ArtworkPalette.From(Fill(16, 16, 0xF9, 0x73, 0x16), 16, 16)!.Value;
        Assert.Equal("#F97316", palette.Primary.Hex);
        Assert.Equal(palette.Primary, palette.Secondary);
    }

    [Fact]
    public void The_larger_area_is_primary()
    {
        var palette = ArtworkPalette.From(Split(16, 16, (0x0E, 0xA5, 0xE9), (0x9F, 0x12, 0x39), topRows: 11), 16, 16)!.Value;
        Assert.Equal("#0EA5E9", palette.Primary.Hex);
        Assert.Equal("#9F1239", palette.Secondary.Hex);
    }

    [Fact]
    public void Grey_black_and_white_are_ignored() => Assert.Null(ArtworkPalette.From(Split(8, 8, (0, 0, 0), (0xFF, 0xFF, 0xFF), 4), 8, 8));

    [Fact]
    public void Empty_pixels_give_nothing() => Assert.Null(ArtworkPalette.From([], 0, 0));

    [Fact]
    public void Grey_with_a_coloured_corner_picks_the_colour()
    {
        var pixels = Fill(16, 16, 0x80, 0x80, 0x80);
        Paint(pixels, 16, 0, 0, 4, 4, (0x30, 0xD1, 0x58));
        Assert.Equal("#30D158", ArtworkPalette.From(pixels, 16, 16)!.Value.Primary.Hex);
    }

    [Fact]
    public void Large_artwork_is_sampled_without_losing_the_colour() =>
        Assert.Equal("#F97316", ArtworkPalette.From(Fill(300, 300, 0xF9, 0x73, 0x16), 300, 300)!.Value.Primary.Hex);

    private static byte[] Fill(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * 4];
        Paint(pixels, width, 0, 0, width, height, (r, g, b));
        return pixels;
    }

    private static byte[] Split(int width, int height, (byte R, byte G, byte B) top, (byte R, byte G, byte B) bottom, int topRows)
    {
        var pixels = new byte[width * height * 4];
        Paint(pixels, width, 0, 0, width, topRows, top);
        Paint(pixels, width, 0, topRows, width, height - topRows, bottom);
        return pixels;
    }

    private static void Paint(byte[] pixels, int stride, int x, int y, int width, int height, (byte R, byte G, byte B) colour)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var i = (row * stride + column) * 4;
                pixels[i] = colour.B;
                pixels[i + 1] = colour.G;
                pixels[i + 2] = colour.R;
                pixels[i + 3] = 0xFF;
            }
        }
    }
}
