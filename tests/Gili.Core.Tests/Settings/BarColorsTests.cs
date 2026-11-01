using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class BarColorsTests
{
    [Fact]
    public void Eight_dark_presets_start_with_the_graphite_default()
    {
        Assert.Equal(8, BarColors.Presets.Count);
        Assert.Equal(("Graphite", "#161618"), BarColors.Presets[0]);
        Assert.Equal(BarColors.Default, new AppSettings().BarColor);
        // White text and icons stay readable on every preset (4.5:1).
        Assert.All(BarColors.Presets, p => Assert.False(BarColors.IsLight(p.Hex), p.Name));
    }

    [Theory]
    [InlineData("#1e2a44", "#1E2A44")]
    [InlineData("1E2A44", "#1E2A44")]
    [InlineData(" #abc ", "#AABBCC")]
    [InlineData("fff", "#FFFFFF")]
    public void Codes_are_read_with_or_without_hash_and_in_short_form(string text, string expected) =>
        Assert.Equal(expected, BarColors.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#1234567")]
    [InlineData("#GG0000")]
    [InlineData("red")]
    [InlineData("# 12345")]
    [InlineData("#12 345")]
    public void Anything_else_is_refused(string text) => Assert.Null(BarColors.Parse(text));

    [Fact]
    public void Light_colors_are_flagged_by_their_contrast_with_white()
    {
        Assert.True(BarColors.IsLight("#FFFFFF"));
        Assert.True(BarColors.IsLight("#8E8E93"));
        Assert.False(BarColors.IsLight("#000000"));
        Assert.False(BarColors.IsLight("#2C3E50"));
    }

    [Fact]
    public void Rgb_splits_the_code() => Assert.Equal((0x16, 0x16, 0x18), BarColors.Rgb("#161618"));

    [Fact]
    public void Preset_name_is_null_for_a_custom_color()
    {
        Assert.Equal("Navy", BarColors.NameOf("#15203D"));
        Assert.Null(BarColors.NameOf("#123456"));
    }

    [Fact]
    public void A_broken_stored_color_falls_back_to_the_default() =>
        Assert.Equal(BarColors.Default, new AppSettings { BarColor = "nope" }.Normalize().BarColor);

    [Fact]
    public void Writing_a_color_row_keeps_valid_codes_only()
    {
        var s = new AppSettings();
        Assert.Equal("#15203D", SettingsCatalog.Write(s, "barColor", "#15203d").BarColor);
        Assert.Equal(BarColors.Default, SettingsCatalog.Write(s, "barColor", "#zz").BarColor);
        Assert.Equal("#161618", SettingsCatalog.Read(s, "barColor"));
    }

    [Fact]
    public void A_light_color_row_warns_but_stays_enabled()
    {
        var light = new AppSettings { BarColor = "#E5E5EA" };
        Assert.Equal(new RowState(true, BarColors.LightNote), SettingsCatalog.State(light, "barColor"));
        Assert.Equal(new RowState(true, null), SettingsCatalog.State(new AppSettings(), "barColor"));
    }
}
