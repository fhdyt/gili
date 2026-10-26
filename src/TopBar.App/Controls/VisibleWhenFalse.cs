using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TopBar.App.Controls;

/// <summary>The inverse of BooleanToVisibilityConverter: visible when the value is false.</summary>
public sealed class VisibleWhenFalse : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
