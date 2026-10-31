using Gili.Core.Devices;
using Gili.Core.Island;

namespace Gili.Core.Tests.Devices;

public class LockKeyRulesTests
{
    [Fact]
    public void Lock_keys_first_reading_is_quiet() => Assert.Empty(LockKeyRules.WideFor(null, new LockKeys(Caps: true, Num: true)));

    [Fact]
    public void Nothing_changed_is_quiet() => Assert.Empty(LockKeyRules.WideFor(new LockKeys(true, false), new LockKeys(true, false)));

    [Fact]
    public void Caps_on_and_off_texts()
    {
        var on = Assert.Single(LockKeyRules.WideFor(new LockKeys(false, false), new LockKeys(true, false)));
        Assert.Equal("caps", on.Key);
        Assert.Equal(WideIcon.Caps, on.Icon);
        Assert.Equal(WideTone.Text, on.Tone);
        Assert.Equal("Caps Lock on", on.Text);
        Assert.Equal(new WideRun("on", WideRunStyle.Strong), on.Runs[^1]);
        Assert.Equal(TimeSpan.FromSeconds(1.5), on.Duration);

        var off = Assert.Single(LockKeyRules.WideFor(new LockKeys(true, false), new LockKeys(false, false)));
        Assert.Equal("Caps Lock off", off.Text);
    }

    [Fact]
    public void Num_lock_has_its_own_key_and_icon()
    {
        var on = Assert.Single(LockKeyRules.WideFor(new LockKeys(false, false), new LockKeys(false, true)));
        Assert.Equal("num", on.Key);
        Assert.Equal(WideIcon.NumLock, on.Icon);
        Assert.Equal("Num Lock on", on.Text);
    }

    [Fact]
    public void Caps_and_num_together_queue_caps_first()
    {
        var both = LockKeyRules.WideFor(new LockKeys(false, false), new LockKeys(true, true));
        Assert.Equal(["caps", "num"], both.Select(w => w.Key));
    }
}
