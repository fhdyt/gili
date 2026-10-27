using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Gili.App.Controls;
using Gili.Core.Notes;

namespace Gili.App.Island;

/// <summary>
/// The note cards as a masonry grid (mockup layoutNotes): each card measured at the column width and put, in the
/// user's order, in the shortest column. Cards sit at the origin and are moved by a TranslateTransform, so a card
/// whose slot changes slides there in 320 ms on the out curve.
/// </summary>
public sealed class NoteGridPanel : Panel
{
    private readonly Dictionary<UIElement, NoteRect> _placed = [];
    private double _arrangedWidth = -1;
    private double _measuredWidth = -1;

    public NoteLayout Layout { get; private set; } = new([], 0);

    /// <summary>The next arrange puts every card in place without sliding: a new search, or the grid shown again.</summary>
    public bool SnapNext { get; set; } = true;

    /// <summary>Where a card rests in the grid (not where it is mid-slide); null for a card not placed yet.</summary>
    public NoteRect? SlotOf(UIElement card) => _placed.TryGetValue(card, out var slot) ? slot : null;

    public double ColumnWidth => NoteGrid.ColumnWidth(_arrangedWidth > 0 ? _arrangedWidth : ActualWidth);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? NoteGrid.MinColumn : availableSize.Width;
        var column = NoteGrid.ColumnWidth(width);
        var heights = new double[InternalChildren.Count];
        for (var i = 0; i < heights.Length; i++)
        {
            InternalChildren[i].Measure(new Size(column, double.PositiveInfinity));
            heights[i] = InternalChildren[i].DesiredSize.Height;
        }
        Layout = NoteGrid.Place(heights, width);
        _measuredWidth = width;
        return new Size(width, Layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Layout.Slots.Count != InternalChildren.Count || finalSize.Width != _measuredWidth)
            Layout = NoteGrid.Place([.. InternalChildren.Cast<UIElement>().Select(c => c.DesiredSize.Height)], finalSize.Width);
        var snap = SnapNext || finalSize.Width != _arrangedWidth || !SystemParameters.ClientAreaAnimation;
        SnapNext = false;
        _arrangedWidth = finalSize.Width;
        var shown = new HashSet<UIElement>();
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var card = InternalChildren[i];
            var slot = Layout.Slots[i];
            card.Arrange(new Rect(0, 0, slot.Width, slot.Height));
            if (card.RenderTransform is not TranslateTransform shift) card.RenderTransform = shift = new TranslateTransform();
            var known = _placed.TryGetValue(card, out var before);
            if (snap || !known)
            {
                Place(shift, TranslateTransform.XProperty, slot.X);
                Place(shift, TranslateTransform.YProperty, slot.Y);
            }
            else
            {
                if (before.X != slot.X) shift.BeginAnimation(TranslateTransform.XProperty, BezierEase.To(slot.X, 320, BezierEase.Out));
                if (before.Y != slot.Y) shift.BeginAnimation(TranslateTransform.YProperty, BezierEase.To(slot.Y, 320, BezierEase.Out));
            }
            _placed[card] = slot;
            shown.Add(card);
        }
        foreach (var gone in _placed.Keys.Where(c => !shown.Contains(c)).ToList()) _placed.Remove(gone);
        return finalSize;
    }

    private static void Place(TranslateTransform shift, DependencyProperty property, double value)
    {
        shift.BeginAnimation(property, null);
        shift.SetValue(property, value);
    }
}
