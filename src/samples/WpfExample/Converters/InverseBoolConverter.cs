using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfExample.Converters;

/// <summary>
/// Converts a boolean value to its inverse.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return DependencyProperty.UnsetValue;
    }
}
