using System.Diagnostics;
using System.Net.NetworkInformation;
using Gili.Core.Modules;

namespace Gili.Platform.Stats;

/// <summary>Upload and download speed from the byte counters of every adapter that is up (PRD 9.2).</summary>
public sealed class NetworkSampler
{
    private long _lastSent = -1;
    private long _lastReceived = -1;
    private long _lastTimestamp;

    public NetworkReading Sample()
    {
        long sent = 0, received = 0;
        var names = new List<string>();
        var up = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .ToList();
        var counted = NetworkAdapters.Counted(up.Select(n => (n.Name, n.Description))).Select(a => a.Name).ToHashSet();
        foreach (var nic in up.Where(n => counted.Contains(n.Name)))
        {
            try
            {
                var stats = nic.GetIPv4Statistics();
                sent += stats.BytesSent;
                received += stats.BytesReceived;
                names.Add(nic.Name);
            }
            catch (NetworkInformationException)
            {
                // An adapter going away mid-enumeration; the next sample skips it.
            }
        }

        var timestamp = Stopwatch.GetTimestamp();
        double upRate = 0, downRate = 0, upBytes = 0, downBytes = 0;
        if (_lastSent >= 0)
        {
            var seconds = Stopwatch.GetElapsedTime(_lastTimestamp, timestamp).TotalSeconds;
            if (seconds > 0)
            {
                // Counters drop when an adapter disconnects; never show a negative speed.
                upBytes = Math.Max(0, sent - _lastSent);
                downBytes = Math.Max(0, received - _lastReceived);
                upRate = upBytes / seconds;
                downRate = downBytes / seconds;
            }
        }
        (_lastSent, _lastReceived, _lastTimestamp) = (sent, received, timestamp);
        return new NetworkReading(upRate, downRate, string.Join(", ", names), upBytes, downBytes);
    }
}
