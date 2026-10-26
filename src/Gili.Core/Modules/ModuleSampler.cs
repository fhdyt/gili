namespace Gili.Core.Modules;

/// <summary>Latest reading of a bar module, or why there is none (the bar shows "—" with the reason as tooltip, PRD 11).</summary>
public readonly record struct ModuleState<T>(T? Value, string? Error) where T : class;

/// <summary>
/// Wraps one provider so its failure only blanks its own module. After a failure the provider is left alone for
/// 5 s, then 30 s, then every 2 min until it works again.
/// </summary>
public sealed class ModuleSampler<T>(Func<T> sample) where T : class
{
    private int _failures;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;
    private ModuleState<T> _state;

    public ModuleState<T> Tick(DateTimeOffset now)
    {
        if (now < _retryAt) return _state;
        try
        {
            _state = new ModuleState<T>(sample(), null);
            _failures = 0;
            _retryAt = DateTimeOffset.MinValue;
        }
        catch (Exception ex)
        {
            _failures++;
            _state = new ModuleState<T>(null, ex.Message);
            _retryAt = now + RetryDelay.After(_failures);
        }
        return _state;
    }
}

public static class RetryDelay
{
    public static TimeSpan After(int failures) => failures switch
    {
        <= 1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMinutes(2),
    };
}
