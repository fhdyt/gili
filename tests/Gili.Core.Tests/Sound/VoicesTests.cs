using Gili.Core.Sound;

namespace Gili.Core.Tests.Sound;

public class VoicesTests
{
    private const int Rate = 44100;

    private static double Rms(float[] samples, double from, double to)
    {
        var a = (int)(from * Rate);
        var b = Math.Min(samples.Length, (int)(to * Rate));
        double sum = 0;
        for (var i = a; i < b; i++) sum += samples[i] * samples[i];
        return Math.Sqrt(sum / Math.Max(1, b - a));
    }

    [Fact]
    public void Five_voices_in_mockup_order() =>
        Assert.Equal(["Radar", "Chime", "Bell", "Soft", "Ping"], Voices.All.Select(v => v.Name));

    [Fact]
    public void Unknown_voice_falls_back_to_chime()
    {
        Assert.Equal("Chime", Voices.Find("nope").Name);
        Assert.Equal("Bell", Voices.Find("bell").Name);
    }

    [Fact]
    public void Tone_rises_in_6_ms_then_decays()
    {
        var tone = new Tone(0, 1000, 0.5, Waveform.Sine, 0.2);
        Assert.Equal(0.1, Voices.Envelope(tone, 0.003), 6);
        Assert.Equal(0.2, Voices.Envelope(tone, 0.006), 6);
        Assert.Equal(0.0001, Voices.Envelope(tone, 0.5), 6);
        Assert.Equal(0.0001, Voices.Envelope(tone, 0.53), 6);
        Assert.Equal(0, Voices.Envelope(tone, -0.001));
    }

    [Fact]
    public void Radar_pattern_is_three_beeps()
    {
        var radar = Voices.Render(Voices.Find("Radar"), Rate);
        Assert.Equal((int)Math.Ceiling((0.28 + 0.11 + 0.05) * Rate), radar.Length);
        Assert.All(new[] { 0.0, 0.14, 0.28 }, start => Assert.True(Rms(radar, start, start + 0.05) > 0.03));
        Assert.True(Rms(radar, 0.115, 0.135) < 0.003);
    }

    [Fact]
    public void Bell_partials_ring_longer_at_low_pitch()
    {
        var bell = Voices.Find("Bell").Tones;
        Assert.Equal(523.25, bell[0].Frequency, 6);
        Assert.Equal(2.2, bell[0].Duration, 6);
        Assert.Equal(2.2 / Math.Pow(8.93, 0.3), bell[3].Duration, 6);
        Assert.Equal(2.5, Voices.Find("Bell").Every);
    }
}
