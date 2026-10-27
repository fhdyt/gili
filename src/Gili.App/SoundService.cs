using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Sound;
using Gili.Platform.Sound;

namespace Gili.App;

/// <summary>
/// Gili's own sounds (PRD 6.6): the card's sound at the alert volume, a preview in Settings, and nothing at all while
/// silent: the player thread and its buffers exist only while something plays (PRD 8.1).
/// </summary>
public sealed class SoundService : IDisposable
{
    private const int SampleRate = 44100;

    private readonly Dispatcher _dispatcher;
    private readonly SettingsHub _settings;
    private readonly FileLog _log;
    private readonly AlertMixer _mixer = new(SampleRate);
    private WaveOutStream? _stream;
    private bool _failureLogged;

    public SoundService(Dispatcher dispatcher, SettingsHub settings, FileLog log)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _log = log;
    }

    /// <summary>
    /// The sound for the card now showing (PRD 6.6 "Kapan berbunyi"), an alarm's own sound for its card; null stops it
    /// with a 100 ms fade.
    /// </summary>
    public void PlayForCard(string? cardId, string? alarmSound = null)
    {
        var s = _settings.Current;
        if (cardId is null || SoundRules.ForCard(cardId, s.TimerSound, alarmSound) is not { } sound)
        {
            Stop();
            return;
        }
        lock (_mixer)
        {
            if (sound.Loop) _mixer.Ring(Voices.Find(sound.Voice), s.AlertVolume / 100.0, sound.Ramp && s.GraduallyIncrease);
            else _mixer.Once(Voices.Find(sound.Voice), s.AlertVolume / 100.0);
        }
        EnsurePlaying();
    }

    /// <summary>Plays a sound once, as Settings does when the timer sound changes.</summary>
    public void Preview(string voice)
    {
        lock (_mixer) _mixer.Once(Voices.Find(voice), _settings.Current.AlertVolume / 100.0);
        EnsurePlaying();
    }

    public void Stop()
    {
        lock (_mixer) _mixer.Stop();
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }

    private int Read(Span<float> buffer)
    {
        lock (_mixer) return _mixer.Read(buffer);
    }

    private void EnsurePlaying()
    {
        if (_stream is not null) return;
        try
        {
            _stream = new WaveOutStream(SampleRate, Read, () => _dispatcher.InvokeAsync(Finished));
        }
        catch (InvalidOperationException ex)
        {
            // No audio device, or it is busy: the card still shows (PRD 6.6). Said once per run.
            if (_failureLogged) return;
            _failureLogged = true;
            _log.Write("Could not play Gili's sound", ex);
        }
    }

    /// <summary>The stream ran out; a sound started in the meantime gets a new stream.</summary>
    private void Finished()
    {
        _stream?.Dispose();
        _stream = null;
        bool playing;
        lock (_mixer) playing = _mixer.IsPlaying;
        if (playing) EnsurePlaying();
    }
}
