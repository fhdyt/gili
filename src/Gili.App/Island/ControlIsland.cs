using System.Windows.Media;

namespace Gili.App.Island;

/// <summary>A control island's content (PRD 6.4): told when it shows and hides, and asks for a new size when it grows.</summary>
public interface IControlIsland
{
    /// <summary>Its island opened or swapped to it: read what it shows now, and start any list reading.</summary>
    void Shown();

    /// <summary>Its island closed or swapped away: stop reacting to list updates.</summary>
    void Hidden();

    /// <summary>Its content changed height, so the island resizes.</summary>
    event Action? Resized;
}

/// <summary>A row of a control island's list (mockup .cv .qn): icon, name, and a status or the accent check.</summary>
public sealed record ControlRowItem(string Name, string Status, Geometry Glyph, bool Active, object? Tag, bool Check = false)
{
    /// <summary>Name and status, read by screen readers.</summary>
    public string Spoken => Status.Length == 0 ? Name : Name + ", " + Status;
}
