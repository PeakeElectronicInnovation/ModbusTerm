using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ModbusTerm.Models;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts connection status to an LED color
    /// </summary>
    public class ConnectionStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionStatus status)
            {
                // Debug output to see what status we're converting

                
                return status switch
                {
                    ConnectionStatus.Connected => new SolidColorBrush(Colors.LimeGreen),
                    ConnectionStatus.MasterConnected => new SolidColorBrush(Colors.DodgerBlue),
                    ConnectionStatus.Failed => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray) // Disconnected or any other state
                };
            }
            

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
