using System;
using System.Globalization;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts between data bits values (7, 8) and ComboBox selection index (0, 1)
    /// </summary>
    public class DataBitsConverter : IValueConverter
    {
        /// <summary>
        /// Converts from data bits value to ComboBox selection index
        /// </summary>
        /// <param name="value">The data bits value (7 or 8)</param>
        /// <param name="targetType">The target type (should be int)</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>ComboBox index (0 for 7 bits, 1 for 8 bits)</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int dataBits)
            {
                return dataBits == 7 ? 0 : 1;
            }
            
            return 1; // Default to 8 data bits (index 1)
        }

        /// <summary>
        /// Converts from ComboBox selection index to data bits value
        /// </summary>
        /// <param name="value">The ComboBox selection index (0 or 1)</param>
        /// <param name="targetType">The target type (should be int)</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Data bits value (7 for index 0, 8 for index 1)</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index == 0 ? 7 : 8;
            }
            
            return 8; // Default to 8 data bits
        }
    }
}
