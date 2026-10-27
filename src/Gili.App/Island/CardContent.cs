using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

namespace Gili.App.Island;

public sealed record CardButton(string Label, bool Primary, Action Run)
{
    public ICommand Command { get; } = new RelayCommand(Run);
}

/// <summary>A card's icon (an Icons.xaml key), colour, title, optional line and buttons (PRD 6.3 "Kartu notifikasi").</summary>
public sealed record CardContent(string Glyph, Color Color, string Title, string? Subtitle, IReadOnlyList<CardButton> Buttons, string? Warning = null);
