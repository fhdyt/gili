using TopBar.Core.Diagnostics;

namespace TopBar.Core.Tests.Diagnostics;

public sealed class FileLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "topbar-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Write_appends_messages_and_exceptions_to_todays_file()
    {
        var log = new FileLog(_dir);
        log.Write("first");
        log.Write("second", new InvalidOperationException("boom"));

        var file = Assert.Single(Directory.GetFiles(_dir, "topbar-*.log"));
        Assert.Equal($"topbar-{DateTime.Now:yyyyMMdd}.log", Path.GetFileName(file));
        var text = File.ReadAllText(file);
        Assert.Contains("first", text);
        Assert.Contains("second", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void DeleteOlderThan_removes_only_old_log_files()
    {
        Directory.CreateDirectory(_dir);
        var now = new DateTime(2026, 10, 26, 12, 0, 0);
        var old = Path.Combine(_dir, "topbar-20261001.log");
        var recent = Path.Combine(_dir, "topbar-20261025.log");
        var other = Path.Combine(_dir, "notes.txt");
        foreach (var f in new[] { old, recent, other }) File.WriteAllText(f, "x");
        File.SetLastWriteTime(old, now.AddDays(-8));
        File.SetLastWriteTime(recent, now.AddDays(-1));
        File.SetLastWriteTime(other, now.AddDays(-30));

        new FileLog(_dir).DeleteOlderThan(TimeSpan.FromDays(7), now);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void DeleteOlderThan_without_a_log_folder_does_nothing() =>
        new FileLog(_dir).DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
}
