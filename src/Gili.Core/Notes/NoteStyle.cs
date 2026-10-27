namespace Gili.Core.Notes;

/// <summary>A note colour: its name (tooltip) and the card fill as 0xRRGGBB.</summary>
public sealed record NoteColor(string Name, uint Rgb);

/// <summary>One text size: its name and the font sizes it gives the editor, the floating note, the card and the "A" in the picker.</summary>
public sealed record NoteSize(string Name, double Editor, double Floating, double CardTitle, double CardBody, double Glyph);

/// <summary>The note colours and text sizes from PRD 6.4 (Notes).</summary>
public static class NoteStyle
{
    public const NoteTextSize Default = NoteTextSize.Medium;

    public static IReadOnlyList<NoteColor> Colors { get; } =
    [
        new("Default", 0x1C1C1E),
        new("Red", 0x5C1F26),
        new("Orange", 0x643013),
        new("Yellow", 0x5C470C),
        new("Green", 0x1D4A31),
        new("Teal", 0x0F4C4B),
        new("Blue", 0x1C3C61),
        new("Purple", 0x42265A),
    ];

    private static readonly NoteSize[] Sizes =
    [
        new("Extra small", 12, 11, 12, 11, 10),
        new("Small", 13, 12, 12.5, 11.5, 11.5),
        new("Medium", 14, 13, 13, 12, 13),
        new("Large", 16, 15, 14.5, 13.5, 15),
    ];

    public static NoteSize Size(NoteTextSize size) => Sizes[Math.Clamp((int)size, 0, Sizes.Length - 1)];

    public static NoteTextSize Step(NoteTextSize size, int delta) => (NoteTextSize)Math.Clamp((int)size + delta, 0, Sizes.Length - 1);

    /// <summary>A colour index outside 0-7 (a database edited by hand) falls back to Default.</summary>
    public static int ClampColor(int color) => color >= 0 && color < Colors.Count ? color : 0;
}
