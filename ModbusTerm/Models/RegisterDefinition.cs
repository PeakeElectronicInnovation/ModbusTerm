namespace ModbusTerm.Models
{
    /// <summary>
    /// Defines a register in the slave mode register table
    /// </summary>
    public class RegisterDefinition
    {
        /// <summary>
        /// Gets or sets the register address
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// Gets or sets the register value
        /// </summary>
        public ushort Value { get; set; }

        /// <summary>
        /// Gets or sets the register name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the register description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data type for this register
        /// </summary>
        public ModbusDataType DataType { get; set; } = ModbusDataType.UInt16;

        /// <summary>
        /// Gets or sets the number of registers used (depends on DataType)
        /// </summary>
        public int RegisterCount 
        { 
            get
            {
                return DataType switch
                {
                    ModbusDataType.UInt16 or ModbusDataType.Int16 or ModbusDataType.Hex or ModbusDataType.Binary => 1,
                    ModbusDataType.UInt32 or ModbusDataType.Int32 or ModbusDataType.Float32 => 2,
                    ModbusDataType.Float64 => 4,
                    _ => 1
                };
            }
        }

        /// <summary>
        /// Gets or sets additional registers for multi-register data types
        /// </summary>
        public List<ushort> AdditionalValues { get; set; } = new List<ushort>();

        /// <summary>
        /// Gets the formatted value based on the data type
        /// </summary>
        public string FormattedValue
        {
            get
            {
                try
                {
                    return DataType switch
                    {
                        ModbusDataType.UInt16 => Value.ToString(),
                        ModbusDataType.Int16 => ((short)Value).ToString(),
                        ModbusDataType.Hex => $"0x{Value:X4}",
                        ModbusDataType.Binary => Convert.ToString(Value, 2).PadLeft(16, '0'),
                        ModbusDataType.UInt32 => GetUInt32Value().ToString(),
                        ModbusDataType.Int32 => GetInt32Value().ToString(),
                        ModbusDataType.Float32 => GetFloat32Value().ToString("F3"),
                        ModbusDataType.Float64 => GetFloat64Value().ToString("F6"),
                        _ => Value.ToString()
                    };
                }
                catch
                {
                    return "Error";
                }
            }
        }

        // Helper methods for multi-register data types
        private uint GetUInt32Value()
        {
            if (AdditionalValues.Count < 1) return Value;
            return (uint)(Value << 16 | AdditionalValues[0]);
        }

        private int GetInt32Value()
        {
            return (int)GetUInt32Value();
        }

        private float GetFloat32Value()
        {
            if (AdditionalValues.Count < 1) return 0;
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(AdditionalValues[0] & 0xFF);
            bytes[1] = (byte)(AdditionalValues[0] >> 8);
            bytes[2] = (byte)(Value & 0xFF);
            bytes[3] = (byte)(Value >> 8);
            return BitConverter.ToSingle(bytes, 0);
        }

        private double GetFloat64Value()
        {
            if (AdditionalValues.Count < 3) return 0;
            byte[] bytes = new byte[8];
            bytes[0] = (byte)(AdditionalValues[2] & 0xFF);
            bytes[1] = (byte)(AdditionalValues[2] >> 8);
            bytes[2] = (byte)(AdditionalValues[1] & 0xFF);
            bytes[3] = (byte)(AdditionalValues[1] >> 8);
            bytes[4] = (byte)(AdditionalValues[0] & 0xFF);
            bytes[5] = (byte)(AdditionalValues[0] >> 8);
            bytes[6] = (byte)(Value & 0xFF);
            bytes[7] = (byte)(Value >> 8);
            return BitConverter.ToDouble(bytes, 0);
        }
    }
}
