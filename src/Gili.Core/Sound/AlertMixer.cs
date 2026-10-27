namespace Gili.Core.Sound;

/// <summary>
/// The sample stream Gili plays (PRD 6.6): one voice at a time, rung on repeat or played once, at the alert volume,
/// with the 30 s rise for alarms, a 100 ms fade when stopped, and an end of its own after 5 minutes.
/// </summary>
public sealed class AlertMixer(int sampleRate)
{
    public static readonly TimeSpan MaxRing = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RampLength = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan FadeLength = TimeSpan.FromMilliseconds(100);

    private float[] _pattern = [];
    private long _period;
    private long _position;
    private long? _fadeFrom;
    private bool _loop;
    private bool _ramp;
    private double _volume;

    public bool IsPlaying { get; private set; }

    /// <summary>Gradually increase: from 30% of the volume up to all of it over 30 s (F-SND-04).</summary>
    public static double Gain(TimeSpan elapsed, double volume, bool ramp) =>
        ramp ? volume * Math.Min(1, 0.3 + 0.7 * elapsed.TotalSeconds / RampLength.TotalSeconds) : volume;

    /// <summary>Rings the voice on repeat, replacing whatever played.</summary>
    public void Ring(Voice voice, double volume, bool ramp) => Start(voice, volume, ramp, loop: true);

    public void Once(Voice voice, double volume) => Start(voice, volume, ramp: false, loop: false);

    /// <summary>Fades out over 100 ms, then the stream ends.</summary>
    public void Stop()
    {
        if (IsPlaying && _fadeFrom is null) _fadeFrom = _position;
    }

    /// <summary>Fills the buffer with the next samples; returns how many it wrote, 0 once the sound has ended.</summary>
    public int Read(Span<float> buffer)
    {
        var fade = (long)(FadeLength.TotalSeconds * sampleRate);
        var limit = (long)(MaxRing.TotalSeconds * sampleRate);
        var written = 0;
        while (written < buffer.Length && IsPlaying)
        {
            if (_loop && _fadeFrom is null && _position >= limit) _fadeFrom = _position;
            var faded = _fadeFrom is { } from ? _position - from : 0;
            if (faded >= fade || (!_loop && _position >= _pattern.Length))
            {
                IsPlaying = false;
                break;
            }
            var index = _loop ? _position % _period : _position;
            var sample = index < _pattern.Length ? _pattern[index] : 0f;
            var gain = Gain(TimeSpan.FromSeconds((double)_position / sampleRate), _volume, _ramp);
            if (_fadeFrom is not null) gain *= 1 - (double)faded / fade;
            buffer[written++] = (float)(sample * gain);
            _position++;
        }
        return written;
    }

    private void Start(Voice voice, double volume, bool ramp, bool loop)
    {
        _pattern = Voices.Render(voice, sampleRate);
        _period = (long)Math.Round(voice.Every * sampleRate);
        _position = 0;
        _fadeFrom = null;
        _loop = loop;
        _ramp = ramp;
        _volume = Math.Clamp(volume, 0, 1);
        IsPlaying = true;
    }
}
