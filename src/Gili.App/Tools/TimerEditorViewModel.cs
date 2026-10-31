using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Timers;

namespace Gili.App.Tools;

/// <summary>
/// The timer editor in Tools (mockup .aedit.lg): a label and a duration, Cancel and Save. Save stays off until the
/// duration reads as one (5, 1:30 or 1:00:00).
/// </summary>
public sealed partial class TimerEditorViewModel : ObservableObject
{
    private readonly LabeledTimer? _existing;
    private readonly Action<string, int> _save;

    [ObservableProperty] private string _label;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDurationInvalid))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _durationText;

    public TimerEditorViewModel(LabeledTimer? existing, Action<string, int> save, Action cancel)
    {
        _existing = existing;
        _save = save;
        _label = existing?.Label ?? "";
        _durationText = existing is null ? "" : TimerText.DurationInput(existing.Clock.TotalSeconds);
        CancelCommand = new RelayCommand(cancel);
    }

    public string Title => _existing is null ? "New timer" : "Edit timer";

    public long? EditingId => _existing?.Id;

    public bool IsDurationInvalid => TimerText.ParseDuration(DurationText) is null;

    public IRelayCommand CancelCommand { get; }

    private bool CanSave() => !IsDurationInvalid;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (TimerText.ParseDuration(DurationText) is { } seconds) _save(Label, seconds);
    }
}
