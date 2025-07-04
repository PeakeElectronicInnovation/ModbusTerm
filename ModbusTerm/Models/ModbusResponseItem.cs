using System;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Represents a single item in a Modbus response
    /// </summary>
    public class ModbusResponseItem
    {
        /// <summary>
        /// Gets or sets the register/coil address
        /// </summary>
        public int Address { get; set; }
        
        /// <summary>
        /// Gets or sets the decimal value
        /// </summary>
        public object Value { get; set; } = 0;
        
        /// <summary>
        /// Gets or sets the data type of the value
        /// </summary>
        public ModbusDataType DataType { get; set; } = ModbusDataType.UInt16;
        
        /// <summary>
        /// Gets or sets the raw register values
        /// </summary>
        public ushort[] RawValues { get; set; } = Array.Empty<ushort>();
        
        /// <summary>
        /// Gets the hexadecimal representation of the raw register values
        /// </summary>
        public string HexValue 
        { 
            get
            {
                if (RawValues == null || RawValues.Length == 0)
                {
                    if (Value is ushort us)
                        return $"0x{us:X4}";
                    else if (Value is bool b)
                        return b ? "0x01" : "0x00";
                    else
                        return string.Empty;
                }
                
                if (RawValues.Length == 1)
                    return $"0x{RawValues[0]:X4}";
                
                string result = "0x";
                foreach (var val in RawValues)
                {
                    result += $"{val:X4} ";
                }
                return result.TrimEnd();
            }
        }
        
        /// <summary>
        /// Gets the binary representation of the raw register values
        /// </summary>
        public string BinaryValue
        {
            get
            {
                if (RawValues == null || RawValues.Length == 0)
                {
                    if (Value is ushort us)
                        return Convert.ToString(us, 2).PadLeft(16, '0');
                    else if (Value is bool b)
                        return b ? "1" : "0";
                    else
                        return string.Empty;
                }
                
                if (RawValues.Length == 1)
                    return Convert.ToString(RawValues[0], 2).PadLeft(16, '0');
                
                string result = "";
                foreach (var val in RawValues)
                {
                    result += Convert.ToString(val, 2).PadLeft(16, '0') + " ";
                }
                return result.TrimEnd();
            }
        }
    }
}
