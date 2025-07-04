using System;
using System.Globalization;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converter to display "Custom..." text for special baud rate value (-1)
    /// </summary>
    public class BaudRateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int baudRate)
            {
                return baudRate == -1 ? "Custom..." : baudRate.ToString();
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
