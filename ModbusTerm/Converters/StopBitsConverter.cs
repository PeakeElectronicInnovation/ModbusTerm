using System;
using System.Globalization;
using System.IO.Ports;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts between StopBits enum values and ComboBox selection index
    /// </summary>
    public class StopBitsConverter : IValueConverter
    {
        /// <summary>
        /// Converts from StopBits enum to ComboBox selection index
        /// </summary>
        /// <param name="value">The StopBits enum value</param>
        /// <param name="targetType">The target type (should be int)</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>ComboBox index (0 for One, 1 for OnePointFive, 2 for Two)</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StopBits stopBits)
            {
                switch (stopBits)
                {
                    case StopBits.One:
                        return 0;
                    case StopBits.OnePointFive:
                        return 1;
                    case StopBits.Two:
                        return 2;
                    default:
                        return 0;
                }
            }
            
            return 0; // Default to One stop bit (index 0)
        }

        /// <summary>
        /// Converts from ComboBox selection index to StopBits enum value
        /// </summary>
        /// <param name="value">The ComboBox selection index</param>
        /// <param name="targetType">The target type (should be StopBits)</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>StopBits enum value (One for index 0, OnePointFive for index 1, Two for index 2)</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                switch (index)
                {
                    case 0:
                        return StopBits.One;
                    case 1:
                        return StopBits.OnePointFive;
                    case 2:
                        return StopBits.Two;
                    default:
                        return StopBits.One;
                }
            }
            
            return StopBits.One; // Default to One stop bit
        }
    }
}
