namespace ModbusTerm.Models
{
    /// <summary>
    /// Data types for Modbus values
    /// </summary>
    public enum ModbusDataType
    {
        /// <summary>
        /// Unsigned 16-bit integer (default)
        /// </summary>
        UInt16 = 0,
        
        /// <summary>
        /// Signed 16-bit integer
        /// </summary>
        Int16 = 1,
        
        /// <summary>
        /// Unsigned 32-bit integer (uses 2 consecutive registers)
        /// </summary>
        UInt32 = 2,
        
        /// <summary>
        /// Signed 32-bit integer (uses 2 consecutive registers)
        /// </summary>
        Int32 = 3,
        
        /// <summary>
        /// 32-bit floating point (uses 2 consecutive registers)
        /// </summary>
        Float32 = 4,
        
        /// <summary>
        /// 64-bit floating point (uses 4 consecutive registers)
        /// </summary>
        Float64 = 5,
        
        /// <summary>
        /// ASCII string (2 characters per register)
        /// </summary>
        AsciiString = 6,
        
        /// <summary>
        /// Hexadecimal representation
        /// </summary>
        Hex = 7,
        
        /// <summary>
        /// Binary representation
        /// </summary>
        Binary = 8
    }
}
