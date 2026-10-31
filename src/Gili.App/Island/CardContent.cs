using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

namespace Gili.App.Island;

public sealed record CardButton(string Label, bool Primary, Action Run)
{
    public ICommand Command { get; } = new RelayCommand(Run);
}

/// <summary>
/// A card's icon (an Icons.xaml key), colour, title, optional line and buttons (PRD 6.3 "Kartu notifikasi"); an alarm's
/// icon also shakes (mockup <c>.cic.shake</c>). <paramref name="Quote"/> is a third line in the text colour (mockup
/// <c>.cs.cq</c>, Claude's question); <paramref name="Pulse"/> is the ring around the icon (mockup <c>.cic.pulse</c>), which
/// reminders and Claude's card don't have.
/// </summary>
public sealed record CardContent(string Glyph, Color Color, string Title, string? Subtitle, IReadOnlyList<CardButton> Buttons, string? Warning = null,
    bool Shake = false, string? Quote = null, bool Filled = false, bool Pulse = true);
