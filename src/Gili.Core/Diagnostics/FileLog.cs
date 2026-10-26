using System.Globalization;

namespace Gili.Core.Diagnostics;

/// <summary>Daily log files gili-yyyyMMdd.log, kept for 7 days (PRD 11).</summary>
public sealed class FileLog(string directory)
{
    private readonly Lock _gate = new();

    public void Write(string message, Exception? exception = null)
    {
        var now = DateTime.Now;
        var entry = $"{now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}";
        if (exception is not null) entry += exception + Environment.NewLine;
        var file = Path.Combine(directory, $"gili-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        lock (_gate)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(file, entry);
        }
    }

    public void DeleteOlderThan(TimeSpan age, DateTime now)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "gili-*.log"))
        {
            if (File.GetLastWriteTime(file) < now - age) File.Delete(file);
        }
    }
}
