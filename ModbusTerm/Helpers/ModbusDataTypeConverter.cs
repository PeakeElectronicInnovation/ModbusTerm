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
                        return "Uint16";
                    case ModbusDataType.Int16:
                        return "Int16";
                    case ModbusDataType.UInt32:
                        return "Uint32";
                    case ModbusDataType.Int32:
                        return "Int32";
                    case ModbusDataType.Float32:
                        return "Float32";
                    case ModbusDataType.Float64:
                        return "Float64";
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
            if (value is string displayText)
            {
                switch (displayText)
                {
                    case "Uint16":
                        return ModbusDataType.UInt16;
                    case "Int16":
                        return ModbusDataType.Int16;
                    case "Uint32":
                        return ModbusDataType.UInt32;
                    case "Int32":
                        return ModbusDataType.Int32;
                    case "Float32":
                        return ModbusDataType.Float32;
                    case "Float64":
                        return ModbusDataType.Float64;
                    case "ASCII":
                        return ModbusDataType.AsciiString;
                    case "Hex":
                        return ModbusDataType.Hex;
                    case "Binary":
                        return ModbusDataType.Binary;
                    default:
                        return ModbusDataType.UInt16; // Default fallback
                }
            }
            
            return ModbusDataType.UInt16; // Default fallback
        }
    }
}
