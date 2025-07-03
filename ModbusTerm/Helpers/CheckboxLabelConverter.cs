using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using ModbusTerm.Models;

namespace ModbusTerm.Helpers
{
    /// <summary>
    /// Converter to change checkbox label and tooltip based on the selected data type
    /// </summary>
    public class CheckboxLabelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Output what we're getting from the binding
            string valuesStr = "";
            for (int i = 0; i < values.Length; i++)
            {
                valuesStr += $"[{i}]:{values[i]?.ToString() ?? "null"}({values[i]?.GetType().Name ?? "null"}) ";
            }
            Debug.WriteLine($"CheckboxLabelConverter values: {valuesStr}");
            
            // Properly handle the ModbusDataType
            ModbusDataType dataType = ModbusDataType.UInt16;
            if (values.Length > 0 && values[0] != null)
            {
                if (values[0] is ModbusDataType dt)
                {
                    dataType = dt;
                    Debug.WriteLine($"CheckboxLabelConverter: Got ModbusDataType directly: {dataType}");
                }
                else if (Enum.TryParse(values[0].ToString(), out ModbusDataType parsedType))
                {
                    dataType = parsedType;
                    Debug.WriteLine($"CheckboxLabelConverter: Parsed ModbusDataType: {dataType}");
                }
                else
                {
                    Debug.WriteLine($"CheckboxLabelConverter: Failed to get ModbusDataType");
                }
            }
            
            // Check if this is for tooltip text
            bool isTooltip = values.Length > 1 && values[1] != null && values[1].ToString() == "True";
            
            Debug.WriteLine($"CheckboxLabelConverter: dataType={dataType}, isTooltip={isTooltip}");
            
            if (dataType == ModbusDataType.AsciiString)
            {
                string result = isTooltip
                    ? "Swap byte order within each Uint16 register when displaying ASCII characters"
                    : "Uint16 Byte Swap";
                Debug.WriteLine($"CheckboxLabelConverter returning: {result}");
                return result;
            }
            else
            {
                string result = isTooltip
                    ? "Enable if most significant Uint16 is sent first (not typical Modbus behavior)"
                    : "Reverse Uint16 order";
                Debug.WriteLine($"CheckboxLabelConverter returning: {result}");
                return result;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
