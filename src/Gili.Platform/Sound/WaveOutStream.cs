using System.Runtime.InteropServices;

namespace Gili.Platform.Sound;

/// <summary>
/// Plays a stream of samples through waveOut on the default device (WAVE_MAPPER), which Windows moves to a new default
/// device by itself (F-SND-06). 44.1 kHz mono 16-bit, 20 ms buffers, at most three queued, so a stop is heard within
/// about 60 ms. A background thread fills the buffers from <c>read</c> until it returns 0, then calls <c>finished</c>.
/// </summary>
public sealed class WaveOutStream : IDisposable
{
    private const int BufferMs = 20;
    private const int Buffers = 3;
    private const uint WAVE_MAPPER = 0xFFFFFFFF;
    private const uint CALLBACK_EVENT = 0x00050000;
    private const int WHDR_DONE = 1;
    private const int HeaderSize = 48;
    private const int FlagsOffset = 24;

    private readonly Func<Span<float>, int> _read;
    private readonly Action _finished;
    private readonly AutoResetEvent _event = new(false);
    private readonly IntPtr _device;
    private readonly IntPtr[] _headers = new IntPtr[Buffers];
    private readonly IntPtr[] _data = new IntPtr[Buffers];
    private readonly bool[] _queued = new bool[Buffers];
    private readonly int _samples;
    private readonly Thread _thread;
    private volatile bool _stopping;

    public WaveOutStream(int sampleRate, Func<Span<float>, int> read, Action finished)
    {
        _read = read;
        _finished = finished;
        _samples = sampleRate * BufferMs / 1000;
        var format = new WaveFormat
        {
            wFormatTag = 1,
            nChannels = 1,
            nSamplesPerSec = (uint)sampleRate,
            wBitsPerSample = 16,
            nBlockAlign = 2,
            nAvgBytesPerSec = (uint)sampleRate * 2,
        };
        var result = waveOutOpen(out _device, WAVE_MAPPER, ref format, _event.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
        if (result != 0) throw new InvalidOperationException($"waveOutOpen failed ({result})");
        for (var i = 0; i < Buffers; i++)
        {
            _data[i] = Marshal.AllocHGlobal(_samples * 2);
            _headers[i] = Marshal.AllocHGlobal(HeaderSize);
            for (var b = 0; b < HeaderSize; b += 8) Marshal.WriteInt64(_headers[i], b, 0);
            Marshal.WriteIntPtr(_headers[i], 0, _data[i]);
            // The length is set once before preparing: a prepared header must not change it.
            Marshal.WriteInt32(_headers[i], 8, _samples * 2);
            waveOutPrepareHeader(_device, _headers[i], HeaderSize);
        }
        _thread = new Thread(Run) { IsBackground = true, Name = "Gili sound" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stopping = true;
        _event.Set();
        if (Thread.CurrentThread != _thread) _thread.Join();
        waveOutReset(_device);
        for (var i = 0; i < Buffers; i++)
        {
            waveOutUnprepareHeader(_device, _headers[i], HeaderSize);
            Marshal.FreeHGlobal(_headers[i]);
            Marshal.FreeHGlobal(_data[i]);
        }
        waveOutClose(_device);
        _event.Dispose();
    }

    private void Run()
    {
        var samples = new float[_samples];
        var pcm = new short[_samples];
        var ended = false;
        while (!_stopping)
        {
            for (var i = 0; i < Buffers && !ended; i++)
            {
                if (_queued[i] && (Marshal.ReadInt32(_headers[i], FlagsOffset) & WHDR_DONE) == 0) continue;
                var count = _read(samples);
                if (count == 0)
                {
                    ended = true;
                    break;
                }
                for (var s = 0; s < _samples; s++)
                    pcm[s] = s < count ? (short)Math.Clamp(samples[s] * short.MaxValue, short.MinValue, short.MaxValue) : (short)0;
                Marshal.Copy(pcm, 0, _data[i], _samples);
                Marshal.WriteInt32(_headers[i], FlagsOffset, Marshal.ReadInt32(_headers[i], FlagsOffset) & ~WHDR_DONE);
                waveOutWrite(_device, _headers[i], HeaderSize);
                _queued[i] = true;
            }
            if (ended && Enumerable.Range(0, Buffers).All(i => !_queued[i] || (Marshal.ReadInt32(_headers[i], FlagsOffset) & WHDR_DONE) != 0))
            {
                _finished();
                return;
            }
            _event.WaitOne(BufferMs);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr device);
}
