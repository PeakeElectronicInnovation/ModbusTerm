using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts a boolean value to a Visibility value
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value
        /// </summary>
        /// <param name="value">The boolean value to convert</param>
        /// <param name="targetType">The target type (should be Visibility)</param>
        /// <param name="parameter">Optional parameter (if "Inverse", will invert the conversion)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverse = parameter?.ToString() == "Inverse";
            bool isVisible = value is bool boolValue && boolValue;
            
            if (isInverse)
                isVisible = !isVisible;
                
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to a boolean value
        /// </summary>
        /// <param name="value">The Visibility value to convert</param>
        /// <param name="targetType">The target type (should be boolean)</param>
        /// <param name="parameter">Optional parameter (if "Inverse", will invert the conversion)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>True if Visibility.Visible, false otherwise</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverse = parameter?.ToString() == "Inverse";
            bool isVisible = value is Visibility visibility && visibility == Visibility.Visible;
            
            if (isInverse)
                isVisible = !isVisible;
                
            return isVisible;
        }
    }
}
