using Gili.Core.Shelf;

namespace Gili.Core.Tests.Shelf;

public class ShelfTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "1 KB")]
    [InlineData(999, "1 KB")]
    [InlineData(340_000, "340 KB")]
    [InlineData(12_400_000, "12.4 MB")]
    [InlineData(2_400_000_000, "2.4 GB")]
    public void Size_follows_fmtSize(long bytes, string expected) => Assert.Equal(expected, ShelfText.Size(bytes));

    [Fact]
    public void Summary_texts()
    {
        Assert.Equal("files on the shelf", ShelfText.Summary(0, 0));
        Assert.Equal("1 file · 2.4 MB", ShelfText.Summary(1, 2_400_000));
        Assert.Equal("3 files · 12.4 MB", ShelfText.Summary(3, 12_400_000));
    }

    [Fact]
    public void Bar_tooltip_texts()
    {
        Assert.Equal("Shelf", ShelfText.BarTooltip(0));
        Assert.Equal("Shelf · 1 file", ShelfText.BarTooltip(1));
        Assert.Equal("Shelf · 3 files", ShelfText.BarTooltip(3));
    }

    [Theory]
    [InlineData(@"C:\report.PDF", "PDF", "#FF6961")]
    [InlineData(@"C:\shot.png", "PNG", "#BF5AF2")]
    [InlineData(@"C:\spreadsheet.xlsx", "XLSX", "#30D158")]
    [InlineData(@"C:\archive.tar.gz", "GZ", "#8E8E93")]
    [InlineData(@"C:\Makefile", "FILE", "#8E8E93")]
    [InlineData(@"C:\movie.webm1234", "WEBM", "#8E8E93")]
    public void Badge_colors_and_text(string path, string text, string color) =>
        Assert.Equal((text, color), ShelfText.Badge(Item(path), isFolder: false));

    [Fact]
    public void Bundle_badge_is_its_count_and_folders_have_none()
    {
        Assert.Equal(("3", "#D1D1D6"), ShelfText.Badge(Item(@"C:\a", @"C:\b", @"C:\c"), isFolder: false));
        Assert.Equal(("", ""), ShelfText.Badge(Item(@"C:\Photos"), isFolder: true));
    }

    private static ShelfItem Item(params string[] paths) => new(1, paths, "x", 0, 0, Now);
}
