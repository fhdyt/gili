namespace Gili.Core.Notes;

/// <summary>Text size of a note; the values are the <c>text_size</c> column (F-TOOL-08).</summary>
public enum NoteTextSize
{
    ExtraSmall = 0,
    Small = 1,
    Medium = 2,
    Large = 3,
}

/// <summary>
/// A note as kept in gili.db: the whole text in <see cref="Body"/>, its first line is the title. <see cref="Float"/> is
/// its window in DIPs while it floats outside the island (F-TOOL-12), null while it is in the island.
/// </summary>
public sealed record Note(long Id, string Body, int Color, NoteTextSize Size, int SortOrder, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, NoteRect? Float = null);
