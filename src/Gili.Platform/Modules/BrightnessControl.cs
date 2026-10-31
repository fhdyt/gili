using System.Management;

namespace Gili.Platform.Modules;

/// <summary>
/// Sets the built-in display's brightness from the Brightness island's slider (F-RIGHT-10). WMI takes about 27 ms a
/// call, so one background task applies only the latest value: dragging never queues up stale levels.
/// </summary>
public sealed class BrightnessControl
{
    private readonly Lock _gate = new();
    private int? _pending;
    private bool _running;

    public void Set(int percent)
    {
        lock (_gate)
        {
            _pending = Math.Clamp(percent, 0, 100);
            if (_running) return;
            _running = true;
        }
        _ = Task.Run(Apply);
    }

    private void Apply()
    {
        while (true)
        {
            int level;
            lock (_gate)
            {
                if (_pending is not { } next)
                {
                    _running = false;
                    return;
                }
                level = next;
                _pending = null;
            }
            Write(level);
        }
    }

    private static void Write(int level)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (var item in searcher.Get())
            {
                using (item) ((ManagementObject)item).InvokeMethod("WmiSetBrightness", [(uint)1, (byte)level]);
            }
        }
        catch (ManagementException)
        {
            // The display lost its brightness control: the module hides on the next read.
        }
    }
}
