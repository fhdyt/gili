using Gili.Core.Taskbar;

namespace Gili.Core.Tests.Taskbar;

public sealed class TaskbarKeeperTests
{
    private sealed class FakeTaskbar : ITaskbar
    {
        public bool AutoHide { get; set; }
        public bool Hidden { get; private set; }
        public List<string> Calls { get; } = [];

        public void SetAutoHide(bool on)
        {
            AutoHide = on;
            Calls.Add($"autohide:{on}");
        }

        public void Hide()
        {
            Hidden = true;
            Calls.Add("hide");
        }

        public void Show()
        {
            Hidden = false;
            Calls.Add("show");
        }
    }

    private sealed class FakeFile : ITaskbarStateFile
    {
        public bool? Saved { get; set; }
        public bool? Read() => Saved;
        public void Write(bool autoHide) => Saved = autoHide;
        public void Delete() => Saved = null;
    }

    private readonly FakeTaskbar _taskbar = new();
    private readonly FakeFile _file = new();

    private TaskbarKeeper Keeper() => new(_taskbar, _file);

    [Fact]
    public void Hiding_saves_the_original_state_before_changing_it()
    {
        Keeper().Apply(hide: true);
        Assert.False(_file.Saved);
        Assert.True(_taskbar.AutoHide);
        Assert.True(_taskbar.Hidden);
        Assert.Equal(["autohide:True", "hide"], _taskbar.Calls);
    }

    [Fact]
    public void Turning_it_off_restores_the_original_state_and_forgets_it()
    {
        var keeper = Keeper();
        keeper.Apply(hide: true);
        keeper.Apply(hide: false);
        Assert.False(_taskbar.AutoHide);
        Assert.False(_taskbar.Hidden);
        Assert.Null(_file.Saved);
    }

    [Fact]
    public void A_taskbar_that_already_auto_hid_stays_auto_hiding_after_restore()
    {
        _taskbar.AutoHide = true;
        var keeper = Keeper();
        keeper.Apply(hide: true);
        keeper.Restore();
        Assert.True(_taskbar.AutoHide);
        Assert.False(_taskbar.Hidden);
    }

    [Fact]
    public void A_state_left_by_a_killed_run_is_the_original_when_hiding_again()
    {
        // The last run hid it and died: Windows now auto-hides, but the user's own choice was "always show".
        _file.Saved = false;
        _taskbar.AutoHide = true;
        Keeper().Apply(hide: true);
        Assert.False(_file.Saved);

        Keeper().Restore();
        Assert.False(_taskbar.AutoHide);
    }

    [Fact]
    public void A_state_left_by_a_killed_run_is_restored_when_the_switch_is_off()
    {
        _file.Saved = false;
        _taskbar.AutoHide = true;
        Assert.True(Keeper().Apply(hide: false));
        Assert.False(_taskbar.AutoHide);
        Assert.Null(_file.Saved);
    }

    [Fact]
    public void Nothing_is_touched_when_gili_never_changed_the_taskbar()
    {
        Assert.False(Keeper().Apply(hide: false));
        Assert.False(Keeper().Restore());
        Assert.Empty(_taskbar.Calls);
    }

    [Fact]
    public void Hiding_twice_writes_the_state_once()
    {
        var keeper = Keeper();
        keeper.Apply(hide: true);
        _taskbar.AutoHide = true;
        keeper.Apply(hide: true);
        Assert.False(_file.Saved);
    }

    [Fact]
    public void An_explorer_restart_hides_the_new_taskbar_only_while_hidden()
    {
        var keeper = Keeper();
        keeper.ShellRestarted();
        Assert.Empty(_taskbar.Calls);

        keeper.Apply(hide: true);
        _taskbar.Calls.Clear();
        keeper.ShellRestarted();
        Assert.Equal(["hide"], _taskbar.Calls);
    }

    [Fact]
    public void Keeper_reports_whether_it_hides_the_taskbar()
    {
        var keeper = Keeper();
        Assert.False(keeper.IsHiding);
        keeper.Apply(hide: true);
        Assert.True(keeper.IsHiding);
        keeper.Restore();
        Assert.False(keeper.IsHiding);
    }
}
