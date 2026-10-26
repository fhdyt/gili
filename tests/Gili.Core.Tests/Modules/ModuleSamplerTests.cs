using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class ModuleSamplerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed record Reading(int N);

    [Fact]
    public void A_successful_sample_is_the_value()
    {
        var state = new ModuleSampler<Reading>(() => new Reading(7)).Tick(T0);
        Assert.Equal(new Reading(7), state.Value);
        Assert.Null(state.Error);
    }

    [Fact]
    public void Failures_show_the_reason_and_retry_after_5s_30s_then_every_2min()
    {
        var calls = 0;
        var sampler = new ModuleSampler<Reading>(() => { calls++; throw new InvalidOperationException("Counter missing"); });

        var state = sampler.Tick(T0);
        Assert.Null(state.Value);
        Assert.Equal("Counter missing", state.Error);

        sampler.Tick(T0.AddSeconds(4.9));
        Assert.Equal(1, calls);
        sampler.Tick(T0.AddSeconds(5));
        Assert.Equal(2, calls);
        sampler.Tick(T0.AddSeconds(34.9));
        Assert.Equal(2, calls);
        sampler.Tick(T0.AddSeconds(35));
        Assert.Equal(3, calls);
        sampler.Tick(T0.AddSeconds(154.9));
        Assert.Equal(3, calls);
        sampler.Tick(T0.AddSeconds(155));
        Assert.Equal(4, calls);
        sampler.Tick(T0.AddSeconds(275));
        Assert.Equal(5, calls);
    }

    [Fact]
    public void Skipped_ticks_keep_reporting_the_error()
    {
        var sampler = new ModuleSampler<Reading>(() => throw new InvalidOperationException("down"));
        sampler.Tick(T0);
        Assert.Equal("down", sampler.Tick(T0.AddSeconds(1)).Error);
    }

    [Fact]
    public void A_success_resets_the_backoff()
    {
        var fail = true;
        var calls = 0;
        var sampler = new ModuleSampler<Reading>(() =>
        {
            calls++;
            return fail ? throw new InvalidOperationException("x") : new Reading(1);
        });

        sampler.Tick(T0);                  // failure 1, next try at +5 s
        sampler.Tick(T0.AddSeconds(5));    // failure 2, next try at +35 s
        fail = false;
        Assert.Equal(new Reading(1), sampler.Tick(T0.AddSeconds(35)).Value);
        fail = true;
        sampler.Tick(T0.AddSeconds(36));   // failure 1 again, next try at +41 s
        sampler.Tick(T0.AddSeconds(41));

        Assert.Equal(5, calls);
    }
}
