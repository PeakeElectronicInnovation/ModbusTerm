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
                        return "u16";
                    case ModbusDataType.Int16:
                        return "i16";
                    case ModbusDataType.UInt32:
                        return "u32";
                    case ModbusDataType.Int32:
                        return "i32";
                    case ModbusDataType.Float32:
                        return "f32";
                    case ModbusDataType.Float64:
                        return "f64";
                    case ModbusDataType.AsciiString:
                        return "ASCII";
                    case ModbusDataType.Hex:
                        return "Hex";
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
