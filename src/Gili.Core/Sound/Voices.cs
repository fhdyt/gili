namespace Gili.Core.Sound;

public enum Waveform { Sine, Triangle }

/// <summary>One note of a voice: when it starts in the pattern, its pitch, how long it rings, its wave and its level.</summary>
public sealed record Tone(double Offset, double Frequency, double Duration, Waveform Shape, double Gain);

/// <summary>A built-in sound: a short pattern of tones that repeats every <paramref name="Every"/> seconds while ringing.</summary>
public sealed record Voice(string Name, double Every, IReadOnlyList<Tone> Tones);

/// <summary>
/// The five built-in sounds (PRD 6.6, F-SND-01), synthesised from the mockup's VOICES, so Gili carries no sound files.
/// </summary>
public static class Voices
{
    private const double Attack = 0.006;
    private const double Floor = 0.0001;
    private const double Tail = 0.05;

    public static IReadOnlyList<Voice> All { get; } =
    [
        new("Radar", 1.2,
        [
            new(0, 1318.5, 0.11, Waveform.Triangle, 0.22),
            new(0.14, 1318.5, 0.11, Waveform.Triangle, 0.22),
            new(0.28, 1568, 0.11, Waveform.Triangle, 0.22),
        ]),
        new("Chime", 2,
        [
            new(0, 1046.5, 1.4, Waveform.Sine, 0.26),
            new(0.18, 1318.5, 1.6, Waveform.Sine, 0.22),
            new(0, 2093, 0.6, Waveform.Sine, 0.05),
        ]),
        new("Bell", 2.5,
        [
            .. new[] { (1.0, 0.3), (2.76, 0.12), (5.4, 0.06), (8.93, 0.03) }
                .Select(p => new Tone(0, 523.25 * p.Item1, 2.2 / Math.Pow(p.Item1, 0.3), Waveform.Sine, p.Item2)),
        ]),
        new("Soft", 2,
        [
            new(0, 659.25, 0.9, Waveform.Sine, 0.16),
            new(0.22, 783.99, 0.9, Waveform.Sine, 0.16),
            new(0.44, 987.77, 0.9, Waveform.Sine, 0.16),
        ]),
        new("Ping", 2,
        [
            new(0, 1760, 0.5, Waveform.Sine, 0.2),
            new(0.09, 2637, 0.35, Waveform.Sine, 0.08),
        ]),
    ];

    public static Voice Find(string? name) =>
        All.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) ?? All[1];

    /// <summary>The mockup's tone(): up linearly to the gain in 6 ms, then down exponentially to 0.0001 at the end.</summary>
    public static double Envelope(Tone tone, double time)
    {
        if (time < 0 || time > tone.Duration + Tail) return 0;
        if (time < Attack) return tone.Gain * time / Attack;
        if (time >= tone.Duration) return Floor;
        return tone.Gain * Math.Pow(Floor / tone.Gain, (time - Attack) / (tone.Duration - Attack));
    }

    /// <summary>One pattern of the voice as samples, from the first tone to 50 ms after the last ends.</summary>
    public static float[] Render(Voice voice, int sampleRate)
    {
        var length = voice.Tones.Max(t => t.Offset + t.Duration + Tail);
        var samples = new float[(int)Math.Ceiling(length * sampleRate)];
        foreach (var tone in voice.Tones)
        {
            var first = (int)(tone.Offset * sampleRate);
            var last = Math.Min(samples.Length, (int)Math.Ceiling((tone.Offset + tone.Duration + Tail) * sampleRate));
            for (var i = first; i < last; i++)
            {
                var time = (double)i / sampleRate - tone.Offset;
                var phase = 2 * Math.PI * tone.Frequency * time;
                var wave = tone.Shape == Waveform.Sine ? Math.Sin(phase) : 2 / Math.PI * Math.Asin(Math.Sin(phase));
                samples[i] += (float)(wave * Envelope(tone, time));
            }
        }
        return samples;
    }
}
