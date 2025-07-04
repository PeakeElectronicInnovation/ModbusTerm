using System;
using System.Globalization;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converter that returns one of two width values based on a boolean input.
    /// The parameter should be in format "width1|width2" where width1 is used when true and width2 when false.
    /// </summary>
    public class BooleanToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            
            // Try to get boolean value
            if (value is bool)
            {
                boolValue = (bool)value;
            }
            
            // Parse the parameter string
            string? param = parameter as string;
            if (string.IsNullOrEmpty(param) || !param.Contains("|"))
            {
                return double.NaN; // Return NaN if parameter is invalid
            }
            
            string[] widths = param.Split('|');
            if (widths.Length != 2)
            {
                return double.NaN;
            }
            
            // Parse width values
            if (double.TryParse(widths[boolValue ? 0 : 1], out double result))
            {
                return result;
            }
            
            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
