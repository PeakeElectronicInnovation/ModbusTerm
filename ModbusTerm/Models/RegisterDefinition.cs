using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Defines a register in the slave mode register table
    /// </summary>
    public class RegisterDefinition : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        private ushort _address;
        private ushort _value;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private ModbusDataType _dataType = ModbusDataType.UInt16;
        private string _editableValue = "0";
        private bool _editingInProgress = false; // Flag to prevent Value setter from disrupting editing
        private bool _suppressNotifications = false; // Flag to control whether property changes raise notifications
        private bool _isRecentlyModified = false; // Flag to indicate this register was recently modified by external master
        
        /// <summary>
        /// Gets or sets the register address
        /// </summary>
        public ushort Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the register value
        /// </summary>
        public ushort Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    var oldValue = _value;
                    _value = value;
                    
                    // Debug output for ASCII strings
                    if (DataType == ModbusDataType.AsciiString)
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG Value setter START: Address={Address}, OldValue=0x{oldValue:X4}, NewValue=0x{_value:X4}, _editingInProgress={_editingInProgress}, AdditionalValues.Count={AdditionalValues.Count}");
                    }
                    
                    // Only update EditableValue if this wasn't triggered by EditableValue setter
                    // This keeps the displayed value in sync when changes come from elsewhere
                    // but prevents disrupting user input in the EditableValue field
                    if (!_editingInProgress)
                    {
                        var newFormattedValue = FormattedValue;
                        var oldEditableValue = _editableValue;
                        _editableValue = newFormattedValue;
                        
                        // Debug output
                        if (DataType == ModbusDataType.AsciiString)
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG Value setter UPDATE: OldEditableValue='{oldEditableValue}', NewFormattedValue='{newFormattedValue}', NewEditableValue='{_editableValue}'");
                        }
                        
                        // Always notify for EditableValue regardless of suppression
                        // This is critical for external writes to show immediately in UI
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditableValue)));
                    }
                    else
                    {
                        if (DataType == ModbusDataType.AsciiString)
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG Value setter SKIPPED: _editingInProgress=true, EditableValue remains '{_editableValue}'");
                        }
                    }
                    
                    // Notify value change
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(FormattedValue)); 
                }
                else
                {
                    if (DataType == ModbusDataType.AsciiString)
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG Value setter NO CHANGE: Value already 0x{_value:X4}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the register value as a string for editing
        /// </summary>
        public string EditableValue
        {
            get => _editableValue;
            set
            {
                if (_editableValue != value)
                {
                    _editableValue = value;
                    
                    // Always notify for EditableValue changes regardless of suppression setting
                    // This ensures updates from external writes always show in UI
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditableValue)));
                    
                    // Set flag to prevent Value setter from overwriting our input
                    _editingInProgress = true;
                    
                    try
                    {
                        // Parse the value based on the data type
                        switch (DataType)
                        {
                            case ModbusDataType.UInt16:
                                if (ushort.TryParse(value, out ushort u16Value))
                                {
                                    Value = u16Value;
                                }
                                break;
                                
                            case ModbusDataType.Int16:
                                if (short.TryParse(value, out short i16Value))
                                {
                                    Value = (ushort)i16Value;
                                }
                                break;
                                
                            case ModbusDataType.Hex:
                                // Try to parse hex with 0x prefix or without
                                string hexVal = value.StartsWith("0x") ? value.Substring(2) : value;
                                if (ushort.TryParse(hexVal, System.Globalization.NumberStyles.HexNumber, null, out ushort hexValue))
                                {
                                    Value = hexValue;
                                }
                                break;
                                
                            case ModbusDataType.Binary:
                                // Parse binary (remove spaces if present)
                                string binVal = value.Replace(" ", "");
                                if (binVal.Length <= 16 && binVal.All(c => c == '0' || c == '1'))
                                {
                                    Value = Convert.ToUInt16(binVal, 2);
                                }
                                break;
                                
                            case ModbusDataType.UInt32:
                                if (uint.TryParse(value, out uint u32Value))
                                {
                                    SetUInt32Value(u32Value);
                                }
                                break;
                                
                            case ModbusDataType.Int32:
                                if (int.TryParse(value, out int i32Value))
                                {
                                    SetInt32Value(i32Value);
                                }
                                break;
                                
                            case ModbusDataType.Float32:
                                if (float.TryParse(value, out float f32Value))
                                {
                                    SetFloat32Value(f32Value);
                                }
                                break;
                                
                            case ModbusDataType.Float64:
                                if (double.TryParse(value, out double f64Value))
                                {
                                    SetFloat64Value(f64Value);
                                }
                                break;
                                
                            case ModbusDataType.AsciiString:
                                // For ASCII strings, directly set the string value
                                SetAsciiStringValue(value);
                                // Notify that RegisterCount may have changed due to string length change
                                NotifyPropertyChanged(nameof(RegisterCount));
                                break;
                        }
                    }
                    catch
                    {
                        // Parsing failed - leave the EditableValue as is but don't update the underlying value
                    }
                    finally
                    {
                        // Always reset the flag when done
                        _editingInProgress = false;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the register name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the register description
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the data type for this register
        /// </summary>
        public ModbusDataType DataType
        {
            get => _dataType;
            set
            {
                if (_dataType != value)
                {
                    var oldType = _dataType;
                    _dataType = value;
                    
                    // Adjust AdditionalValues based on the new data type
                    int requiredRegisters = RegisterCount;
                    
                    // First clear any unnecessary additional values if switching to a smaller type
                    if (requiredRegisters <= 1)
                    {
                        // Single register types don't need AdditionalValues
                        AdditionalValues.Clear();
                    }
                    else if (AdditionalValues.Count > (requiredRegisters - 1))
                    {
                        // Trim AdditionalValues to the required size
                        while (AdditionalValues.Count > (requiredRegisters - 1))
                        {
                            AdditionalValues.RemoveAt(AdditionalValues.Count - 1);
                        }
                    }
                    else
                    {
                        // Add zeros if we need more registers
                        while (AdditionalValues.Count < (requiredRegisters - 1))
                        {
                            AdditionalValues.Add(0);
                        }
                    }
                    
                    // For data format changes (e.g., from UInt16 to Int16), the raw Value stays the same
                    // but the formatted interpretation changes
                    
                    // Update EditableValue to reflect the new format
                    _editableValue = FormattedValue;
                    
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(RegisterCount));
                    NotifyPropertyChanged(nameof(FormattedValue));
                    NotifyPropertyChanged(nameof(EditableValue));
                }
            }
        }

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
                    ModbusDataType.AsciiString => GetAsciiStringRegisterCount(),
                    _ => 1
                };
            }
        }

        /// <summary>
        /// Gets or sets additional registers for multi-register data types
        /// </summary>
        public List<ushort> AdditionalValues { get; set; } = new List<ushort>();

        /// <summary>
        /// Calculate the number of registers required for an ASCII string
        /// </summary>
        /// <returns>Number of registers needed (2 characters per register)</returns>
        private int GetAsciiStringRegisterCount()
        {
            if (DataType != ModbusDataType.AsciiString)
                return 1;

            // Get the current string value from EditableValue
            string stringValue = _editableValue ?? "";
            
            // Calculate registers needed (2 characters per register, round up)
            int registerCount = (stringValue.Length + 1) / 2;
            
            // Minimum of 1 register
            return Math.Max(1, registerCount);
        }

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
                        ModbusDataType.AsciiString => GetAsciiStringValue(),
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
            // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
            return (uint)(AdditionalValues[0] << 16 | Value);
        }

        private void SetUInt32Value(uint value)
        {
            // Ensure we have enough space in AdditionalValues
            while (AdditionalValues.Count < 1)
            {
                AdditionalValues.Add(0);
            }
            
            // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
            Value = (ushort)(value & 0xFFFF); // Low word in Value
            AdditionalValues[0] = (ushort)(value >> 16); // High word in AdditionalValues[0]
            
            // Notify about Value changes to trigger slave service update
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(FormattedValue));
        }

        private int GetInt32Value()
        {
            return (int)GetUInt32Value();
        }
        
        private void SetInt32Value(int value)
        {
            SetUInt32Value((uint)value);
        }

        private float GetFloat32Value()
        {
            if (AdditionalValues.Count < 1) return 0;
            byte[] bytes = new byte[4];
            // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
            bytes[0] = (byte)(Value & 0xFF);  // Low word bytes
            bytes[1] = (byte)(Value >> 8);
            bytes[2] = (byte)(AdditionalValues[0] & 0xFF);  // High word bytes
            bytes[3] = (byte)(AdditionalValues[0] >> 8);
            return BitConverter.ToSingle(bytes, 0);
        }
        
        private void SetFloat32Value(float value)
        {
            // Ensure we have enough space in AdditionalValues
            while (AdditionalValues.Count < 1)
            {
                AdditionalValues.Add(0);
            }
            
            // Convert float to bytes
            byte[] bytes = BitConverter.GetBytes(value);
            
            // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
            Value = (ushort)((bytes[1] << 8) | bytes[0]);  // Low word
            AdditionalValues[0] = (ushort)((bytes[3] << 8) | bytes[2]);  // High word
            
            // Notify about Value changes to trigger slave service update
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(FormattedValue));
        }

        private double GetFloat64Value()
        {
            if (AdditionalValues.Count < 3) return 0;
            byte[] bytes = new byte[8];
            // Match Float32 pattern: each register stores bytes in big-endian, registers in little-endian order
            bytes[0] = (byte)(Value & 0xFF);        // Low byte of word 0
            bytes[1] = (byte)(Value >> 8);          // High byte of word 0
            bytes[2] = (byte)(AdditionalValues[0] & 0xFF);  // Low byte of word 1
            bytes[3] = (byte)(AdditionalValues[0] >> 8);    // High byte of word 1
            bytes[4] = (byte)(AdditionalValues[1] & 0xFF);  // Low byte of word 2
            bytes[5] = (byte)(AdditionalValues[1] >> 8);    // High byte of word 2
            bytes[6] = (byte)(AdditionalValues[2] & 0xFF);  // Low byte of word 3
            bytes[7] = (byte)(AdditionalValues[2] >> 8);    // High byte of word 3
            
            var result = BitConverter.ToDouble(bytes, 0);
            System.Diagnostics.Debug.WriteLine($"GetFloat64Value: Value=0x{Value:X4}, Add[0]=0x{AdditionalValues[0]:X4}, Add[1]=0x{AdditionalValues[1]:X4}, Add[2]=0x{AdditionalValues[2]:X4} => {result}");
            return result;
        }
        
        private void SetFloat64Value(double value)
        {
            // Ensure we have enough space in AdditionalValues
            while (AdditionalValues.Count < 3)
            {
                AdditionalValues.Add(0);
            }
            
            // Convert double to bytes
            byte[] bytes = BitConverter.GetBytes(value);
            
            // Match Float32 pattern: each register stores bytes in big-endian, registers in little-endian order
            Value = (ushort)((bytes[1] << 8) | bytes[0]);                    // Word 0: bytes 0-1 with byte swap
            AdditionalValues[0] = (ushort)((bytes[3] << 8) | bytes[2]);      // Word 1: bytes 2-3 with byte swap
            AdditionalValues[1] = (ushort)((bytes[5] << 8) | bytes[4]);      // Word 2: bytes 4-5 with byte swap
            AdditionalValues[2] = (ushort)((bytes[7] << 8) | bytes[6]);      // Word 3: bytes 6-7 with byte swap
            
            System.Diagnostics.Debug.WriteLine($"SetFloat64Value: {value} => Value=0x{Value:X4}, Add[0]=0x{AdditionalValues[0]:X4}, Add[1]=0x{AdditionalValues[1]:X4}, Add[2]=0x{AdditionalValues[2]:X4}");
            
            // Notify about Value changes to trigger slave service update
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(FormattedValue));
        }

        private string GetAsciiStringValue()
        {
            try
            {
                var chars = new List<char>();
                
                // Extract characters from Value (first register) - first char from high byte, second char from low byte
                if ((Value >> 8) != 0)
                {
                    char c = (char)(Value >> 8);
                    chars.Add(c); // First char from high byte
                }
                if ((Value & 0xFF) != 0)
                {
                    char c = (char)(Value & 0xFF);
                    chars.Add(c); // Second char from low byte
                }
                
                // Extract characters from AdditionalValues - first char from high byte, second char from low byte
                for (int i = 0; i < AdditionalValues.Count; i++)
                {
                    var reg = AdditionalValues[i];
                    if ((reg >> 8) != 0)
                    {
                        char c = (char)(reg >> 8);
                        chars.Add(c); // First char from high byte
                    }
                    if ((reg & 0xFF) != 0)
                    {
                        char c = (char)(reg & 0xFF);
                        chars.Add(c); // Second char from low byte
                    }
                }
                
                // Remove null terminators and return as string
                var result = new string(chars.Where(c => c != '\0').ToArray());
                return result;
            }
            catch
            {
                return "";
            }
        }
        
        private void SetAsciiStringValue(string value)
        {
            try
            {
                // Pad string to even length
                if (value.Length % 2 != 0)
                    value += "\0";
                
                // Calculate required registers
                int requiredRegisters = Math.Max(1, (value.Length + 1) / 2);
                
                // Ensure we have enough space in AdditionalValues
                while (AdditionalValues.Count < (requiredRegisters - 1))
                {
                    AdditionalValues.Add(0);
                }
                
                // Trim if we have too many
                while (AdditionalValues.Count > (requiredRegisters - 1))
                {
                    AdditionalValues.RemoveAt(AdditionalValues.Count - 1);
                }
                
                // Set the first register (Value) - first char in high byte, second char in low byte
                if (value.Length >= 1)
                {
                    ushort firstReg = (ushort)(value[0] << 8); // First char in high byte
                    if (value.Length >= 2)
                        firstReg |= (ushort)value[1]; // Second char in low byte
                    Value = firstReg;
                }
                else
                {
                    Value = 0;
                }
                
                // Set additional registers - first char in high byte, second char in low byte
                for (int i = 0; i < AdditionalValues.Count; i++)
                {
                    int charIndex = (i + 1) * 2;
                    ushort reg = 0;
                    
                    if (charIndex < value.Length)
                        reg = (ushort)(value[charIndex] << 8); // First char in high byte
                    if (charIndex + 1 < value.Length)
                        reg |= (ushort)value[charIndex + 1]; // Second char in low byte
                        
                    AdditionalValues[i] = reg;
                }
                
                // Update the formatted value and notify that Value changed to trigger slave service update
                NotifyPropertyChanged(nameof(Value));
                NotifyPropertyChanged(nameof(FormattedValue));
            }
            catch
            {
                // On error, clear to empty string
                Value = 0;
                AdditionalValues.Clear();
            }
        }
        
        /// <summary>
        /// Gets or sets whether property change notifications should be suppressed
        /// </summary>
        public bool SuppressNotifications
        {
            get => _suppressNotifications;
            set => _suppressNotifications = value;
        }
        
        /// <summary>
        /// Gets or sets whether this register was recently modified by an external Modbus master
        /// Used for highlighting in the UI
        /// </summary>
        public bool IsRecentlyModified
        {
            get => _isRecentlyModified;
            set
            {
                if (_isRecentlyModified != value)
                {
                    _isRecentlyModified = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (!_suppressNotifications)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        /// <summary>
        /// Forces a PropertyChanged event regardless of SuppressNotifications setting
        /// </summary>
        /// <param name="propertyName">Name of the property to force notification for</param>
        public void ForcePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
