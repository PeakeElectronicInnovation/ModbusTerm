using System;
using System.Collections.Generic;
using System.ComponentModel;
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
                    _value = value;
                    
                    // Only update EditableValue if this wasn't triggered by EditableValue setter
                    // This keeps the displayed value in sync when changes come from elsewhere
                    // but prevents disrupting user input in the EditableValue field
                    if (!_editingInProgress)
                    {
                        _editableValue = FormattedValue;
                        
                        // Always notify for EditableValue regardless of suppression
                        // This is critical for external writes to show immediately in UI
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditableValue)));
                    }
                    
                    // Notify value change
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(FormattedValue)); 
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
                                // Not implemented yet for EditableValue - would need UI changes
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
            
            // Update the formatted value
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
            
            // Update the formatted value
            NotifyPropertyChanged(nameof(FormattedValue));
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
        
        private void SetFloat64Value(double value)
        {
            // Ensure we have enough space in AdditionalValues
            while (AdditionalValues.Count < 3)
            {
                AdditionalValues.Add(0);
            }
            
            // Convert double to bytes
            byte[] bytes = BitConverter.GetBytes(value);
            
            // Set the values
            AdditionalValues[2] = (ushort)((bytes[1] << 8) | bytes[0]);
            AdditionalValues[1] = (ushort)((bytes[3] << 8) | bytes[2]);
            AdditionalValues[0] = (ushort)((bytes[5] << 8) | bytes[4]);
            Value = (ushort)((bytes[7] << 8) | bytes[6]);
            
            // Update the formatted value
            NotifyPropertyChanged(nameof(FormattedValue));
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
