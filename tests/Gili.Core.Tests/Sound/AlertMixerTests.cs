using Gili.Core.Sound;

namespace Gili.Core.Tests.Sound;

public class AlertMixerTests
{
    private const int Rate = 44100;

    private static float[] Read(AlertMixer mixer, int count)
    {
        var buffer = new float[count];
        var written = mixer.Read(buffer);
        return buffer[..written];
    }

    [Fact]
    public void Ring_repeats_every_period()
    {
        var mixer = new AlertMixer(Rate);
        mixer.Ring(Voices.Find("Chime"), volume: 1, ramp: false);
        var samples = Read(mixer, 2 * Rate + 2000);
        for (var k = 100; k < 2000; k += 97) Assert.Equal(samples[k], samples[2 * Rate + k], 5);
    }

    [Fact]
    public void Ramp_starts_at_30_percent_and_reaches_full_at_30_s()
    {
        Assert.Equal(0.3 * 0.8, AlertMixer.Gain(TimeSpan.Zero, 0.8, ramp: true), 6);
        Assert.Equal(0.65 * 0.8, AlertMixer.Gain(TimeSpan.FromSeconds(15), 0.8, ramp: true), 6);
        Assert.Equal(0.8, AlertMixer.Gain(TimeSpan.FromSeconds(30), 0.8, ramp: true), 6);
        Assert.Equal(0.8, AlertMixer.Gain(TimeSpan.FromSeconds(90), 0.8, ramp: true), 6);
        Assert.Equal(0.8, AlertMixer.Gain(TimeSpan.Zero, 0.8, ramp: false), 6);
    }

    [Fact]
    public void Ring_stops_by_itself_after_5_minutes()
    {
        var mixer = new AlertMixer(Rate);
        mixer.Ring(Voices.Find("Radar"), 1, ramp: false);
        var total = 0L;
        var buffer = new float[Rate];
        int written;
        while ((written = mixer.Read(buffer)) > 0) total += written;
        Assert.InRange(total, 300L * Rate, 300L * Rate + Rate / 10 + 1);
        Assert.False(mixer.IsPlaying);
    }

    [Fact]
    public void Stop_fades_out_in_100_ms()
    {
        var mixer = new AlertMixer(Rate);
        mixer.Ring(Voices.Find("Chime"), 1, ramp: false);
        Read(mixer, 1000);
        mixer.Stop();
        var fade = Read(mixer, Rate);
        Assert.Equal(Rate / 10, fade.Length);
        Assert.True(fade.Skip(fade.Length - 100).Max(Math.Abs) < 0.02);
        Assert.Empty(Read(mixer, 100));
        Assert.False(mixer.IsPlaying);
    }

    [Fact]
    public void A_new_ring_replaces_the_old()
    {
        var mixer = new AlertMixer(Rate);
        mixer.Ring(Voices.Find("Radar"), 1, ramp: false);
        Read(mixer, 5000);
        mixer.Ring(Voices.Find("Chime"), 1, ramp: false);
        var chime = Voices.Render(Voices.Find("Chime"), Rate);
        var samples = Read(mixer, 3000);
        Assert.Equal(chime[..3000], samples);
    }

    [Fact]
    public void Once_plays_one_pattern_then_ends()
    {
        var mixer = new AlertMixer(Rate);
        mixer.Once(Voices.Find("Ping"), 1);
        Assert.Equal(Voices.Render(Voices.Find("Ping"), Rate).Length, Read(mixer, 3 * Rate).Length);
        Assert.Empty(Read(mixer, 100));
    }

    [Fact]
    public void Zero_volume_is_silence()
    {
        var mixer = new AlertMixer(Rate);
        mixer.Ring(Voices.Find("Radar"), 0, ramp: false);
        Assert.All(Read(mixer, Rate), s => Assert.Equal(0f, s));
        Assert.True(mixer.IsPlaying);
    }
}
