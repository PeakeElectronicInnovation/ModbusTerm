using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility values with inverse logic.
    /// True -> Collapsed, False -> Visible
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Inverse logic: true becomes Collapsed, false becomes Visible
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            // Default to Visible for non-boolean values
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                // Inverse logic: Collapsed becomes true, Visible becomes false
                return visibility == Visibility.Collapsed;
            }
            
            return false;
        }
    }
}
