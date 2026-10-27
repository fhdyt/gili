using Gili.Core.Clipboard;

namespace Gili.Core.Tests.Clipboard;

public class ClipboardSnippetTests
{
    private static ClipboardContent Content(
        string? text = null, string[]? files = null, bool cut = false, bool image = false, int w = 0, int h = 0,
        string? html = null, string? url = null, string? owner = null, bool sensitive = false) =>
        new(sensitive, text, files ?? [], cut, image, w, h, html, url, owner);

    private static (int W, int H)? NoImage(string path) => null;

    [Fact]
    public void Text_shows_the_first_line_in_quotes_cut_at_40()
    {
        var s = ClipboardSnippets.For(Content("Meeting at 3 in the Merapi room, bring a charger\nsecond"), NoImage)!;
        Assert.Equal(SnippetKind.Text, s.Kind);
        Assert.Equal("\"Meeting at 3 in the Merapi room, bring …\"", s.Text);
    }

    [Fact]
    public void Text_tooltip_stops_at_120()
    {
        var s = ClipboardSnippets.For(Content(new string('a', 200)), NoImage)!;
        Assert.Equal("Copied: " + new string('a', 119) + "…", s.Tooltip);
    }

    [Fact]
    public void Blank_text_shows_nothing()
    {
        Assert.Null(ClipboardSnippets.For(Content(" \r\n\t"), NoImage));
        Assert.Null(ClipboardSnippets.For(Content(), NoImage));
    }

    [Fact]
    public void Several_files_show_a_count()
    {
        var s = ClipboardSnippets.For(Content(files: [@"C:\a.pdf", @"C:\d\b.xlsx", @"C:\c.png"]), NoImage)!;
        Assert.Equal((SnippetKind.Files, "3 files", "Copied: a.pdf, b.xlsx, c.png"), (s.Kind, s.Text, s.Tooltip));
    }

    [Fact]
    public void One_file_shows_its_name_cut_at_32()
    {
        var s = ClipboardSnippets.For(Content(files: [@"C:\docs\quarterly-report-final-version-2026.pdf"]), NoImage)!;
        Assert.Equal("quarterly-report-final-version-…", s.Text);
        Assert.Equal(@"Copied: quarterly-report-final-version-2026.pdf", s.Tooltip);
    }

    [Fact]
    public void Cut_files_say_cut()
    {
        var s = ClipboardSnippets.For(Content(files: [@"C:\a.pdf", @"C:\b.pdf", @"C:\c.png"], cut: true), NoImage)!;
        Assert.Equal((SnippetKind.Cut, "3 files", "Cut: a.pdf, b.pdf, c.png"), (s.Kind, s.Text, s.Tooltip));
    }

    [Fact]
    public void One_image_file_shows_like_an_image()
    {
        var s = ClipboardSnippets.For(Content(files: [@"C:\pics\bali-sunset.jpg"]), _ => (1920, 1080))!;
        Assert.Equal((SnippetKind.Image, "bali-sunset.jpg", "1920 × 1080"), (s.Kind, s.Text, s.Detail));
        Assert.Equal("Image copied · bali-sunset.jpg · 1920 × 1080", s.Tooltip);
    }

    [Fact]
    public void Copied_image_shows_its_name_and_size()
    {
        var s = ClipboardSnippets.For(Content(image: true, w: 800, h: 600, url: "https://x/y/bali-sunset.jpg?w=1"), NoImage)!;
        Assert.Equal((SnippetKind.Image, "bali-sunset.jpg", "800 × 600"), (s.Kind, s.Text, s.Detail));
    }

    [Fact]
    public void Sensitive_content_is_hidden()
    {
        var s = ClipboardSnippets.For(Content("hunter2", sensitive: true), NoImage)!;
        Assert.Equal((SnippetKind.Hidden, "Copied (hidden)", "Hidden because the app marked it as sensitive"), (s.Kind, s.Text, s.Tooltip));
    }

    [Fact]
    public void Image_name_prefers_the_file_then_the_url_then_screenshot_then_image()
    {
        Assert.Equal("cat.png", ClipboardSnippets.ImageName(Content(image: true, files: [@"C:\p\cat.png"], url: "https://x/dog.png")));
        Assert.Equal("bali-sunset.jpg", ClipboardSnippets.ImageName(Content(image: true, url: "https://x/y/bali-sunset.jpg?w=1")));
        Assert.Equal("b.png", ClipboardSnippets.ImageName(Content(image: true, html: "<html><img alt=\"\" src=\"https://x/a/b.png\"></html>")));
        Assert.Equal("Screenshot", ClipboardSnippets.ImageName(Content(image: true, owner: "SnippingTool")));
        Assert.Equal("Screenshot", ClipboardSnippets.ImageName(Content(image: true)));
        Assert.Equal("Image", ClipboardSnippets.ImageName(Content(image: true, owner: "Photoshop")));
    }
}
