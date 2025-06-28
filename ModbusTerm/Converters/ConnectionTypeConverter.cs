using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using ModbusTerm.Models;

namespace ModbusTerm.Converters
{
    /// <summary>
    /// Converts between ConnectionType enum values and boolean values for radio buttons
    /// </summary>
    public class ConnectionTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"ConnectionTypeConverter.Convert - Value: {value}, Parameter: {parameter}");
            
            if (value is ConnectionType connectionType && parameter is string typeStr)
            {
                // Check if the connection type matches the expected type
                ConnectionType expectedType = Enum.Parse<ConnectionType>(typeStr);
                bool result = connectionType == expectedType;
                Debug.WriteLine($"ConnectionTypeConverter.Convert - ConnectionType: {connectionType}, ExpectedType: {expectedType}, Result: {result}");
                return result;
            }
            
            Debug.WriteLine("ConnectionTypeConverter.Convert - No match, returning false");
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"ConnectionTypeConverter.ConvertBack - Value: {value}, Parameter: {parameter}");
            
            if (value is bool isChecked && parameter is string typeStr)
            {
                // Important: Convert back both when checked AND unchecked for proper radio group behavior
                if (isChecked)
                {
                    ConnectionType result = Enum.Parse<ConnectionType>(typeStr);
                    Debug.WriteLine($"ConnectionTypeConverter.ConvertBack - Checked: {isChecked}, TypeStr: {typeStr}, Result: {result}");
                    return result;
                }
            }
            
            Debug.WriteLine("ConnectionTypeConverter.ConvertBack - No action, returning UnsetValue");
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}
