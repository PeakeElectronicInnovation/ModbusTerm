using System;
using System.Globalization;
using System.IO.Ports;
using System.Windows.Data;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts between Parity enum values and ComboBox selection index
    /// </summary>
    public class ParityConverter : IValueConverter
    {
        /// <summary>
        /// Converts from Parity enum to ComboBox selection index
        /// </summary>
        /// <param name="value">The Parity enum value</param>
        /// <param name="targetType">The target type (should be int)</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>ComboBox index (0 for None, 1 for Even, 2 for Odd, etc.)</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Parity parity)
            {
                switch (parity)
                {
                    case Parity.None:
                        return 0;
                    case Parity.Even:
                        return 1;
                    case Parity.Odd:
                        return 2;
                    case Parity.Mark:
                        return 3;
                    case Parity.Space:
                        return 4;
                    default:
                        return 0;
                }
            }
            
            return 0; // Default to None (index 0)
        }

        /// <summary>
        /// Converts from ComboBox selection index to Parity enum value
        /// </summary>
        /// <param name="value">The ComboBox selection index</param>
        /// <param name="targetType">The target type (should be Parity)</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Parity enum value</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                switch (index)
                {
                    case 0:
                        return Parity.None;
                    case 1:
                        return Parity.Even;
                    case 2:
                        return Parity.Odd;
                    case 3:
                        return Parity.Mark;
                    case 4:
                        return Parity.Space;
                    default:
                        return Parity.None;
                }
            }
            
            return Parity.None; // Default to None
        }
    }
}
