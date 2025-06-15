namespace ModbusTerm.Models
{
    /// <summary>
    /// Enumeration of supported Modbus function codes
    /// </summary>
    public enum ModbusFunctionCode : byte
    {
        ReadCoils = 1,                // FC01
        ReadDiscreteInputs = 2,       // FC02
        ReadHoldingRegisters = 3,     // FC03
        ReadInputRegisters = 4,       // FC04
        WriteSingleCoil = 5,          // FC05
        WriteSingleRegister = 6,      // FC06
        WriteMultipleCoils = 15,      // FC15
        WriteMultipleRegisters = 16   // FC16
    }

    // ModbusDataType enum is defined in ModbusDataType.cs

    /// <summary>
    /// Base class for Modbus function parameters
    /// </summary>
    public class ModbusFunctionParameters
    {
        /// <summary>
        /// Gets or sets the function code
        /// </summary>
        public ModbusFunctionCode FunctionCode { get; set; }

        /// <summary>
        /// Gets or sets the slave device ID/address (1-247)
        /// </summary>
        public byte SlaveId { get; set; } = 1;

        /// <summary>
        /// Gets or sets the starting address for the operation
        /// </summary>
        public ushort StartAddress { get; set; } = 0;

        /// <summary>
        /// Gets or sets the desired data type for displaying results
        /// </summary>
        public ModbusDataType DataType { get; set; } = ModbusDataType.UInt16;

        /// <summary>
        /// Gets or sets the quantity of registers or coils to read/write
        /// </summary>
        public ushort Quantity { get; set; } = 1;

        /// <summary>
        /// Checks if this is a read function
        /// </summary>
        public bool IsReadFunction => 
            FunctionCode == ModbusFunctionCode.ReadCoils ||
            FunctionCode == ModbusFunctionCode.ReadDiscreteInputs ||
            FunctionCode == ModbusFunctionCode.ReadHoldingRegisters ||
            FunctionCode == ModbusFunctionCode.ReadInputRegisters;

        /// <summary>
        /// Checks if this is a write function
        /// </summary>
        public bool IsWriteFunction => !IsReadFunction;

        /// <summary>
        /// Checks if this function operates on coils (boolean data)
        /// </summary>
        public bool IsCoilFunction =>
            FunctionCode == ModbusFunctionCode.ReadCoils ||
            FunctionCode == ModbusFunctionCode.ReadDiscreteInputs ||
            FunctionCode == ModbusFunctionCode.WriteSingleCoil ||
            FunctionCode == ModbusFunctionCode.WriteMultipleCoils;

        /// <summary>
        /// Checks if this function operates on registers (16-bit data)
        /// </summary>
        public bool IsRegisterFunction => !IsCoilFunction;
    }

    /// <summary>
    /// Parameters for read functions (FC01, FC02, FC03, FC04)
    /// </summary>
    public class ReadFunctionParameters : ModbusFunctionParameters
    {
    }

    /// <summary>
    /// Parameters for writing a single coil (FC05)
    /// </summary>
    public class WriteSingleCoilParameters : ModbusFunctionParameters
    {
        /// <summary>
        /// Gets or sets the boolean value to write (true = ON, false = OFF)
        /// </summary>
        public bool Value { get; set; } = false;

        public WriteSingleCoilParameters()
        {
            FunctionCode = ModbusFunctionCode.WriteSingleCoil;
        }
    }

    /// <summary>
    /// Parameters for writing a single register (FC06)
    /// </summary>
    public class WriteSingleRegisterParameters : ModbusFunctionParameters
    {
        /// <summary>
        /// Gets or sets the 16-bit value to write
        /// </summary>
        public ushort Value { get; set; } = 0;

        public WriteSingleRegisterParameters()
        {
            FunctionCode = ModbusFunctionCode.WriteSingleRegister;
        }
    }

    /// <summary>
    /// Parameters for writing multiple coils (FC15)
    /// </summary>
    public class WriteMultipleCoilsParameters : ModbusFunctionParameters
    {
        /// <summary>
        /// Gets or sets the boolean values to write
        /// </summary>
        public List<bool> Values { get; set; } = new List<bool>();

        public WriteMultipleCoilsParameters()
        {
            FunctionCode = ModbusFunctionCode.WriteMultipleCoils;
        }
    }

    /// <summary>
    /// Parameters for writing multiple registers (FC16)
    /// </summary>
    public class WriteMultipleRegistersParameters : ModbusFunctionParameters
    {
        /// <summary>
        /// Gets or sets the 16-bit values to write
        /// </summary>
        public List<ushort> Values { get; set; } = new List<ushort>();

        public WriteMultipleRegistersParameters()
        {
            FunctionCode = ModbusFunctionCode.WriteMultipleRegisters;
        }
    }
}
