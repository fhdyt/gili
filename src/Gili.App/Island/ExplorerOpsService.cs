using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using Gili.Core.FileOps;
using Gili.Platform.Apps;

namespace Gili.App.Island;

/// <summary>The answers the conflict card passes on to Explorer's dialog (PRD 6.3 "Konflik file").</summary>
public enum FileOpAnswer { Replace, Skip, Retry }

/// <summary>
/// Explorer's copy, move and delete progress (F-ISL-15): Explorer's progress dialog (<c>OperationStatusWindow</c>) is noticed
/// when it shows, then read with UI Automation twice a second on a worker thread, only while it exists. Nothing is clicked
/// unless the user answers a conflict from the island's card.
/// </summary>
public sealed class ExplorerOpsService : IDisposable
{
    public const string DialogClass = "OperationStatusWindow";

    private readonly FileOpTracker _tracker = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private nint _dialog;
    private bool _reading;
    private bool _demo;

    public ExplorerOpsService()
    {
        _poll.Tick += (_, _) => Poll();
    }

    public FileOpSlot? Slot => _tracker.Slot;

    /// <summary>The operation waiting for a decision, if any.</summary>
    public FileOp? Conflict => _tracker.Conflict;

    /// <summary>The slot or the conflict changed (on the UI thread).</summary>
    public event Action? Changed;

    /// <summary>A dialog already open when Gili starts is followed too.</summary>
    public void Start()
    {
        var dialog = FindWindow(DialogClass, null);
        if (dialog != 0) Follow(dialog);
    }

    /// <summary>Any top-level window that shows; only Explorer's progress dialog starts the reading.</summary>
    public void OnWindowShown(nint hwnd, string className)
    {
        if (className == DialogClass) Follow(hwnd);
    }

    /// <summary>Brings Explorer's dialog to the front, as a click on the slot does (PRD 6.3).</summary>
    public void ShowDialog()
    {
        if (_dialog != 0 && IsWindow(_dialog)) TaskbarWindows.BringToFront(_dialog);
    }

    /// <summary>Presses Replace, Skip or Try again in Explorer's dialog; false when the button can't be pressed.</summary>
    public async Task<bool> AnswerAsync(FileOpAnswer answer)
    {
        var dialog = _dialog;
        if (_demo || dialog == 0) return _demo;
        return await Task.Run(() =>
        {
            try
            {
                var root = AutomationElement.FromHandle(dialog);
                var button = answer switch
                {
                    FileOpAnswer.Replace => ById(root, "IdTileKeepSource"),
                    FileOpAnswer.Skip => ById(root, "IdTileKeepDest") ?? ByName(root, "Skip"),
                    _ => ByName(root, "Try Again"),
                };
                if (button?.GetCurrentPattern(InvokePattern.Pattern) is not InvokePattern invoke) return false;
                invoke.Invoke();
                return true;
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException or ElementNotEnabledException)
            {
                return false;
            }
        });
    }

    /// <summary>The demo's operations instead of Explorer's (F-BAR-09); null ends them.</summary>
    public void SetDemo(IReadOnlyList<FileOpTile>? tiles)
    {
        _demo = tiles is not null;
        Apply([.. (tiles ?? []).Select(FileOpRules.Parse)]);
        if (_tracker.Slot is not null) _poll.Start();
    }

    private void Follow(nint dialog)
    {
        _dialog = dialog;
        _poll.Start();
        Poll();
    }

    private async void Poll()
    {
        if (_demo)
        {
            if (_tracker.Tick(DateTimeOffset.Now)) Changed?.Invoke();
            if (_tracker.Slot is null) _poll.Stop();
            return;
        }
        if (_reading) return;
        var dialog = _dialog;
        if (dialog == 0 || !IsWindow(dialog) || !IsWindowVisible(dialog))
        {
            Apply([]);
            if (_tracker.Slot is null)
            {
                _dialog = 0;
                _poll.Stop();
            }
            return;
        }
        _reading = true;
        try
        {
            var tiles = await Task.Run(() => Read(dialog));
            if (tiles is not null) Apply([.. tiles.Select(FileOpRules.Parse)]);
        }
        finally
        {
            _reading = false;
        }
    }

    private void Apply(IReadOnlyList<FileOp> ops)
    {
        if (_tracker.Update(ops, DateTimeOffset.Now)) Changed?.Invoke();
    }

    /// <summary>Each operation tile in the dialog; null when the dialog couldn't be read this time.</summary>
    private static List<FileOpTile>? Read(nint dialog)
    {
        try
        {
            var root = AutomationElement.FromHandle(dialog);
            var tiles = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "OperationTileDialog"));
            var list = new List<FileOpTile>();
            foreach (AutomationElement tile in tiles)
            {
                string? Text(string id) => ById(tile, id)?.Current.Name;
                var names = tile.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                    .Cast<AutomationElement>().Select(b => (b.Current.AutomationId, b.Current.Name)).ToList();
                list.Add(new FileOpTile(
                    tile.Current.Name,
                    Text("eltItemName"),
                    Text("eltItemsRemaining"),
                    Text("eltConflictInterruptDescription"),
                    CanReplace: names.Any(n => n.AutomationId == "IdTileKeepSource"),
                    CanSkip: names.Any(n => n.AutomationId == "IdTileKeepDest" || n.Name.StartsWith("Skip", StringComparison.Ordinal)),
                    CanRetry: names.Any(n => n.Name.StartsWith("Try Again", StringComparison.OrdinalIgnoreCase)),
                    NeedsAdmin: names.Any(n => n.Name.Equals("Continue", StringComparison.Ordinal))));
            }
            return list;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException or ArgumentException)
        {
            return null;
        }
    }

    private static AutomationElement? ById(AutomationElement root, string id) =>
        root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    private static AutomationElement? ByName(AutomationElement root, string prefix) =>
        root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            .Cast<AutomationElement>().FirstOrDefault(b => b.Current.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public void Dispose() => _poll.Stop();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);
}
