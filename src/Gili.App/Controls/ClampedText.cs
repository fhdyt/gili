using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Gili.App.Controls;

/// <summary>
/// Wrapped text cut after <see cref="MaxLines"/> lines with "…" on the last one, like CSS -webkit-line-clamp; a
/// TextBlock with MaxHeight drops whole lines instead and never marks a cut at a line break.
/// </summary>
public sealed class ClampedText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ClampedText), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(
        nameof(MaxLines), typeof(int), typeof(ClampedText), new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(ClampedText), new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(ClampedText), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(ClampedText), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(ClampedText), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    private FormattedText? _formatted;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? double.NaN : Math.Max(1, availableSize.Width);
        var limit = Math.Max(1, MaxLines) * LineHeight + 0.5;
        var formatted = Format(Text, width);
        if (formatted.Height > limit)
        {
            // The longest start of the text that fits with "…" after it, so a cut on a line break is marked too.
            var (low, high) = (0, Text.Length);
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (Format(Cut(mid), width).Height <= limit) low = mid;
                else high = mid - 1;
            }
            formatted = Format(Cut(low), width);
        }
        _formatted = formatted;
        return new Size(double.IsNaN(width) ? formatted.WidthIncludingTrailingWhitespace : width, formatted.Height);
    }

    private string Cut(int length) => Text[..length].TrimEnd() + "…";

    private FormattedText Format(string text, double width)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), FontSize, Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            LineHeight = LineHeight,
        };
        if (!double.IsNaN(width)) formatted.MaxTextWidth = width;
        return formatted;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_formatted is null) return;
        _formatted.SetForegroundBrush(Foreground);
        drawingContext.DrawText(_formatted, new Point(0, 0));
    }
}
