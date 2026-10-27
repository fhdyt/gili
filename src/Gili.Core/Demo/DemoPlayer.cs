namespace Gili.Core.Demo;

/// <summary>Hands out the demo's steps as their time comes, each once.</summary>
public sealed class DemoPlayer(IReadOnlyList<DemoStep> steps)
{
    private int _next;

    public bool IsDone => _next >= steps.Count;

    public IReadOnlyList<DemoAction> Due(TimeSpan elapsed)
    {
        var due = new List<DemoAction>();
        while (_next < steps.Count && steps[_next].At <= elapsed) due.Add(steps[_next++].Action);
        return due;
    }
}
