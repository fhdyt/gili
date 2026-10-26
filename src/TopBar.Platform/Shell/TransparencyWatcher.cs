using Windows.UI.ViewManagement;

namespace TopBar.Platform.Shell;

/// <summary>Follows Windows' "Transparency effects" switch; when it is off the bar is drawn solid (F-BAR-06).</summary>
public sealed class TransparencyWatcher : IDisposable
{
    private readonly UISettings _settings = new();

    public TransparencyWatcher() => _settings.AdvancedEffectsEnabledChanged += OnChanged;

    public bool Enabled => _settings.AdvancedEffectsEnabled;

    /// <summary>Raised on a background thread.</summary>
    public event Action<bool>? Changed;

    public void Dispose() => _settings.AdvancedEffectsEnabledChanged -= OnChanged;

    private void OnChanged(UISettings sender, object args) => Changed?.Invoke(sender.AdvancedEffectsEnabled);
}
