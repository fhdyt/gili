namespace Gili.Core.Taskbar;

/// <summary>The primary monitor's Windows taskbar (Shell_TrayWnd) and Windows' own auto-hide setting.</summary>
public interface ITaskbar
{
    bool AutoHide { get; }

    void SetAutoHide(bool on);

    void Hide();

    void Show();
}

/// <summary>The taskbar's auto-hide state from before Gili changed it, kept on disk so a killed run can still restore it.</summary>
public interface ITaskbarStateFile
{
    bool? Read();

    void Write(bool autoHide);

    void Delete();
}

/// <summary>
/// Hide Windows taskbar (F-BAR-11, PRD 6.1): auto-hide gives its space back, hiding the window keeps it from sliding in
/// at the bottom edge. The user's own auto-hide choice is written before anything changes and only a restore removes
/// it, so a crash or a forced kill leaves it for the next start (design 3f).
/// </summary>
public sealed class TaskbarKeeper(ITaskbar taskbar, ITaskbarStateFile file)
{
    public bool IsHiding { get; private set; }

    /// <summary>At start and whenever the switch changes. True when the taskbar changed.</summary>
    public bool Apply(bool hide)
    {
        if (!hide) return Restore();
        if (file.Read() is null) file.Write(taskbar.AutoHide);
        if (!taskbar.AutoHide) taskbar.SetAutoHide(true);
        taskbar.Hide();
        IsHiding = true;
        return true;
    }

    /// <summary>Switch off, Quit, logoff and crash. Does nothing, and returns false, when Gili never changed the taskbar.</summary>
    public bool Restore()
    {
        IsHiding = false;
        if (file.Read() is not { } original) return false;
        taskbar.Show();
        if (taskbar.AutoHide != original) taskbar.SetAutoHide(original);
        file.Delete();
        return true;
    }

    /// <summary>Explorer restarted with a new taskbar window; Windows kept the auto-hide setting.</summary>
    public void ShellRestarted()
    {
        if (IsHiding) taskbar.Hide();
    }
}
