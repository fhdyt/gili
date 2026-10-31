using Gili.Core.Formatting;

namespace Gili.Core.Tests.Formatting;

public class StatTextTests
{
    private const double KB = 1024;
    private const double MB = 1024 * 1024;
    private const double GB = 1024 * 1024 * 1024;

    [Theory]
    [InlineData(12.4, "12%")]
    [InlineData(12.6, "13%")]
    [InlineData(-3, "0%")]
    [InlineData(140, "100%")]
    public void Percent_is_a_whole_number_between_0_and_100(double value, string expected) =>
        Assert.Equal(expected, StatText.Percent(value));

    [Theory]
    [InlineData(113702154240UL, "106 GB")]
    [InlineData(10UL * 1024 * 1024 * 1024, "10 GB")]
    [InlineData(9019431321UL, "8.4 GB")]
    [InlineData(0UL, "0.0 GB")]
    public void Disk_uses_whole_gigabytes_from_ten_up(ulong bytes, string expected)
    {
        Assert.Equal(expected, StatText.Disk(bytes));
        Assert.Equal(expected + " free", StatText.DiskFree(bytes));
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(-5, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(1023.9, "1023 B/s")]
    [InlineData(2 * KB, "2 KB/s")]
    [InlineData(999 * KB, "999 KB/s")]
    [InlineData(999.6 * KB, "1.0 MB/s")]
    [InlineData(2.1 * MB, "2.1 MB/s")]
    [InlineData(150 * MB, "150.0 MB/s")]
    public void Rate_picks_B_KB_or_MB(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, StatText.Rate(bytesPerSecond));

    [Theory]
    [InlineData(8.6 * GB, 16 * GB, "8.6 / 16 GB")]
    [InlineData(8.6 * GB, 15.7 * GB, "8.6 / 15.7 GB")]
    public void MemoryUsage_shows_used_over_total_in_GB(double used, double total, string expected) =>
        Assert.Equal(expected, StatText.MemoryUsage((ulong)used, (ulong)total));
}
