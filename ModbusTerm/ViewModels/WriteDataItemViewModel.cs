using ModbusTerm.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModbusTerm.ViewModels
{
    /// <summary>
    /// ViewModel for an individual write data item with its own value and data type
    /// </summary>
    public class WriteDataItemViewModel : ViewModelBase
    {
        private string _value = string.Empty;
        private string _name = string.Empty;
        private ModbusDataType _selectedDataType;
        private ObservableCollection<ModbusDataType> _availableDataTypes;
        private bool _isCoilWrite;
        private bool _booleanValue;
        private int _index; // Index in the collection for address calculation
        
        /// <summary>
        /// Gets or sets the string value for this write data item
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        
        /// <summary>
        /// Gets or sets the name for this write data item
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Gets or sets the index in the collection for address calculation
        /// </summary>
        public int Index
        {
            get => _index;
            set
            {
                if (SetProperty(ref _index, value))
                {
                    OnPropertyChanged(nameof(Address));
                }
            }
        }

        /// <summary>
        /// Gets the calculated address based on the start address and index
        /// </summary>
        public int Address { get; set; }

        /// <summary>
        /// Gets or sets the boolean value for coil operations
        /// </summary>
        public bool BooleanValue
        {
            get => _booleanValue;
            set 
            {
                if (SetProperty(ref _booleanValue, value))
                {
                    // Update the string value to match the boolean value
                    _value = value.ToString();
                    OnPropertyChanged(nameof(Value));
                }
            }
        }
        
        /// <summary>
        /// Gets whether this is a coil write operation
        /// </summary>
        public bool IsCoilWrite => _isCoilWrite;
        
        /// <summary>
        /// Gets or sets the selected data type for this write data item
        /// </summary>
        public ModbusDataType SelectedDataType
        {
            get => _selectedDataType;
            set 
            {
                if (SetProperty(ref _selectedDataType, value))
                {
                    // Notify parent that data type has changed, which may affect quantity calculation
                    OnDataTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the collection of available data types for this write data item
        /// </summary>
        public ObservableCollection<ModbusDataType> AvailableDataTypes
        {
            get => _availableDataTypes;
            set => SetProperty(ref _availableDataTypes, value);
        }
        
        /// <summary>
        /// Event that is raised when the data type changes
        /// </summary>
        public event EventHandler? OnDataTypeChanged;
        
        /// <summary>
        /// Initializes a new instance of the WriteDataItemViewModel class
        /// </summary>
        /// <param name="isCoilWrite">True if this is for a coil write operation</param>
        /// <param name="index">Index in the collection (used for address calculation)</param>
        /// <param name="startAddress">Starting address for the Modbus request</param>
        /// <param name="name">Optional name for this item</param>
        public WriteDataItemViewModel(bool isCoilWrite = false, int index = 0, int startAddress = 0, string name = "")
        {
            _availableDataTypes = new ObservableCollection<ModbusDataType>();
            _isCoilWrite = isCoilWrite;
            _index = index;
            _name = name;
            Address = startAddress + index;
            
            // Initialize data types based on whether it's a coil or register write
            UpdateAvailableDataTypes(isCoilWrite);
            
            // Set default value and data type
            if (isCoilWrite)
            {
                _booleanValue = false;
                _value = "False";
                _selectedDataType = ModbusDataType.Binary;
            }
            else
            {
                _value = "0";
                _selectedDataType = ModbusDataType.UInt16;
            }
        }
        
        /// <summary>
        /// Updates the address based on a new start address
        /// </summary>
        /// <param name="startAddress">The new start address</param>
        public void UpdateAddress(int startAddress)
        {
            Address = startAddress + _index;
            OnPropertyChanged(nameof(Address));
        }
        
        /// <summary>
        /// Updates the available data types based on whether this is for a coil or register write
        /// </summary>
        /// <param name="isCoilWrite">True if this is for a coil write operation</param>
        public void UpdateAvailableDataTypes(bool isCoilWrite)
        {
            AvailableDataTypes.Clear();
            
            if (isCoilWrite)
            {
                // For coil writes, only binary/boolean data types are valid
                AvailableDataTypes.Add(ModbusDataType.Binary);
            }
            else
            {
                // For register writes, all numeric data types are valid
                AvailableDataTypes.Add(ModbusDataType.UInt16);
                AvailableDataTypes.Add(ModbusDataType.Int16);
                AvailableDataTypes.Add(ModbusDataType.UInt32);
                AvailableDataTypes.Add(ModbusDataType.Int32);
                AvailableDataTypes.Add(ModbusDataType.Float32);
                AvailableDataTypes.Add(ModbusDataType.Float64);
                AvailableDataTypes.Add(ModbusDataType.AsciiString);
            }
            
            // If current selected data type is not valid anymore, reset to a valid one
            if (!AvailableDataTypes.Contains(_selectedDataType))
            {
                SelectedDataType = AvailableDataTypes[0];
            }
        }
        
        /// <summary>
        /// Gets the number of registers needed for this data item based on its data type
        /// </summary>
        public int GetRegisterCount()
        {
            return _selectedDataType switch
            {
                ModbusDataType.UInt16 => 1,
                ModbusDataType.Int16 => 1,
                ModbusDataType.UInt32 => 2,
                ModbusDataType.Int32 => 2,
                ModbusDataType.Float32 => 2,
                ModbusDataType.Float64 => 4,
                ModbusDataType.AsciiString => 1, // This is per register, might need special handling for strings
                _ => 1
            };
        }
    }
}
