using System.ComponentModel;
using System.Runtime.InteropServices;
using Gili.Core.Modules;

namespace Gili.Platform.Stats;

/// <summary>
/// GPU use from every <c>GPU Engine</c> instance through one PDH wildcard query (PRD 9.2), which keeps up with processes
/// that come and go; <see cref="GpuEngines"/> adds them up like Task Manager. About 2 ms a read with ~470 instances
/// (plan 3c). The first read after creation is 0.
/// </summary>
public sealed class GpuSampler : IDisposable
{
    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_FMT_NOCAP100 = 0x00008000;
    private const int PDH_MORE_DATA = unchecked((int)0x800007D2);
    private const uint PDH_CSTATUS_NEW_DATA = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PDH_FMT_COUNTERVALUE
    {
        public uint CStatus;
        public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public IntPtr Name;
        public PDH_FMT_COUNTERVALUE Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr items);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr query);

    private IntPtr _query;
    private IntPtr _counter;
    private IntPtr _buffer;
    private uint _capacity;

    public GpuReading Sample()
    {
        try
        {
            if (_query == IntPtr.Zero)
            {
                Open();
                return new GpuReading(0);
            }
            Check(PdhCollectQueryData(_query));
            return new GpuReading(GpuEngines.Utilization(Read()));
        }
        catch
        {
            // Open a fresh query on the next try instead of reusing a broken one.
            Close();
            throw;
        }
    }

    public void Dispose() => Close();

    private void Open()
    {
        Check(PdhOpenQueryW(null, IntPtr.Zero, out _query));
        Check(PdhAddEnglishCounterW(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _counter));
        Check(PdhCollectQueryData(_query));
    }

    private List<(string, double)> Read()
    {
        var size = _capacity;
        var result = PdhGetFormattedCounterArrayW(_counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out var count, _buffer);
        if (result == PDH_MORE_DATA)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = Marshal.AllocHGlobal((int)size);
            _capacity = size;
            result = PdhGetFormattedCounterArrayW(_counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out count, _buffer);
        }
        Check(result);
        var items = new List<(string, double)>((int)count);
        var stride = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();
        for (var i = 0; i < count; i++)
        {
            var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(_buffer + i * stride);
            if (item.Value.CStatus > PDH_CSTATUS_NEW_DATA) continue;
            items.Add((Marshal.PtrToStringUni(item.Name) ?? "", item.Value.DoubleValue));
        }
        return items;
    }

    private void Close()
    {
        if (_query != IntPtr.Zero) PdhCloseQuery(_query);
        _query = IntPtr.Zero;
        _counter = IntPtr.Zero;
        Marshal.FreeHGlobal(_buffer);
        _buffer = IntPtr.Zero;
        _capacity = 0;
    }

    private static void Check(int result)
    {
        if (result != 0) throw new Win32Exception(result, $"GPU counters are unavailable (PDH 0x{result:X8})");
    }
}
