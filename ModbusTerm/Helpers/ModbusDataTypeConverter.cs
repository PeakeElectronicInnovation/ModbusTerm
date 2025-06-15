using ModbusTerm.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace ModbusTerm.Helpers
{
    /// <summary>
    /// Converter to display friendly names for ModbusDataType enum values
    /// </summary>
    public class ModbusDataTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ModbusDataType dataType)
            {
                switch (dataType)
                {
                    case ModbusDataType.UInt16:
                        return "Unsigned 16-bit";
                    case ModbusDataType.Int16:
                        return "Signed 16-bit";
                    case ModbusDataType.UInt32:
                        return "Unsigned 32-bit";
                    case ModbusDataType.Int32:
                        return "Signed 32-bit";
                    case ModbusDataType.Float32:
                        return "Float 32-bit";
                    case ModbusDataType.Float64:
                        return "Float 64-bit";
                    case ModbusDataType.AsciiString:
                        return "ASCII String";
                    case ModbusDataType.Hex:
                        return "Hexadecimal";
                    case ModbusDataType.Binary:
                        return "Binary";
                    default:
                        return dataType.ToString();
                }
            }
            
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This method is not needed for this converter
            throw new NotImplementedException();
        }
    }
}
