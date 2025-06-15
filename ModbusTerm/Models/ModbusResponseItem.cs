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
        /// Gets the hexadecimal representation of the value
        /// </summary>
        public string HexValue 
        { 
            get
            {
                if (Value is ushort us)
                    return $"0x{us:X4}";
                else if (Value is bool b)
                    return b ? "0x01" : "0x00";
                else
                    return string.Empty;
            }
        }
        
        /// <summary>
        /// Gets the binary representation of the value
        /// </summary>
        public string BinaryValue
        {
            get
            {
                if (Value is ushort us)
                    return Convert.ToString(us, 2).PadLeft(16, '0');
                else if (Value is bool b)
                    return b ? "1" : "0";
                else
                    return string.Empty;
            }
        }
    }
}
