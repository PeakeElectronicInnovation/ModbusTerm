using ModbusTerm.Helpers;
using ModbusTerm.Models;
using ModbusTerm.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace ModbusTerm.ViewModels
{
    /// <summary>
    /// Main ViewModel for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IModbusService _masterService;
        private readonly ModbusSlaveService _slaveService;
        private IModbusService? _currentService;
        private ConnectionParameters _connectionParameters;
        private bool _isConnected;
        private bool _isMasterMode = true;
        private ModbusFunctionParameters _currentRequest;
        private ModbusResponseInfo? _lastResponse;
        private ObservableCollection<ModbusResponseItem> _responseItems = new ObservableCollection<ModbusResponseItem>();
        private string _responseStatus = "-";
        private string _responseTime = "- ms";
        private ModbusDataType _selectedDataType = ModbusDataType.UInt16;
        private bool _autoScrollEventLog = true;
        private ObservableCollection<WriteDataItemViewModel> _writeDataInputs = new ObservableCollection<WriteDataItemViewModel>();
        private List<ModbusDataType> _availableDataTypes = new List<ModbusDataType>();
        private ICommand _addWriteDataItemCommand;
        private ICommand _removeWriteDataItemCommand;

        /// <summary>
        /// Command to connect to a Modbus device
        /// </summary>
        public RelayCommand ConnectCommand { get; }

        /// <summary>
        /// Command to disconnect from a Modbus device
        /// </summary>
        public RelayCommand DisconnectCommand { get; }

        /// <summary>
        /// Command to send a Modbus request
        /// </summary>
        public RelayCommand SendRequestCommand { get; }

        /// <summary>
        /// Command to clear the event log
        /// </summary>
        public RelayCommand ClearEventsCommand { get; }

        /// <summary>
        /// Command to export the event log
        /// </summary>
        public RelayCommand ExportEventsCommand { get; }

        /// <summary>
        /// Command to save connection parameters
        /// </summary>
        public RelayCommand SaveConnectionCommand { get; }

        /// <summary>
        /// Command to load connection parameters
        /// </summary>
        public RelayCommand LoadConnectionCommand { get; }

        /// <summary>
        /// Gets the command to add a new write data item
        /// </summary>
        public ICommand AddWriteDataItemCommand => _addWriteDataItemCommand ??= new RelayCommand(
            execute: _ => AddWriteDataItem(),
            canExecute: _ => IsMultipleWriteFunction);

        /// <summary>
        /// Gets the command to remove a write data item
        /// </summary>
        public ICommand RemoveWriteDataItemCommand => _removeWriteDataItemCommand ??= new RelayCommand(
            execute: parameter => RemoveWriteDataItem(parameter as WriteDataItemViewModel),
            canExecute: parameter => IsMultipleWriteFunction && _writeDataInputs.Count > 1);

        /// <summary>
        /// Gets or sets whether the connection is established
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        /// <summary>
        /// Gets or sets whether the application is in master mode
        /// </summary>
        public bool IsMasterMode
        {
            get => _isMasterMode;
            set
            {
                if (SetProperty(ref _isMasterMode, value))
                {
                    // Update connection parameters
                    if (_connectionParameters != null)
                    {
                        _connectionParameters.IsMaster = value;
                    }

                    OnPropertyChanged(nameof(IsSlaveMode));
                    OnPropertyChanged(nameof(ConnectionParameters));
                }
            }
        }

        /// <summary>
        /// Gets whether the application is in slave mode
        /// </summary>
        public bool IsSlaveMode => !IsMasterMode;

        /// <summary>
        /// Gets or sets the connection parameters
        /// </summary>
        public ConnectionParameters ConnectionParameters
        {
            get => _connectionParameters;
            set => SetProperty(ref _connectionParameters, value);
        }

        /// <summary>
        /// Gets or sets the current Modbus function parameters
        /// </summary>
        public ModbusFunctionParameters CurrentRequest
        {
            get => _currentRequest;
            set
            {
                if (SetProperty(ref _currentRequest, value))
                {
                    // Update available data types when the function changes
                    UpdateAvailableDataTypes();

                    // Update write data inputs
                    UpdateWriteDataInputs();

                    // Notify property changes
                    OnPropertyChanged(nameof(IsWriteFunction));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the event log should automatically scroll to the bottom
        /// </summary>
        public bool AutoScrollEventLog
        {
            get => _autoScrollEventLog;
            set => SetProperty(ref _autoScrollEventLog, value);
        }

        /// <summary>
        /// Gets or sets the last Modbus response
        /// </summary>
        public ModbusResponseInfo LastResponse
        {
            get => _lastResponse;
            set
            {
                if (SetProperty(ref _lastResponse, value))
                {
                    // Clear existing response items
                    ResponseItems.Clear();

                    // Update response status and time
                    ResponseStatus = value?.IsSuccess == true ? "Success" : $"Error: {value?.ErrorMessage ?? "Unknown error"}";
                    ResponseTime = value != null ? $"{value.ExecutionTimeMs} ms" : "- ms";

                    // Process response data into displayable items
                    if (value?.Data != null)
                    {
                        ReformatResponseData(value.Data);
                    }

                    // Notify UI that HasLastResponse has changed
                    OnPropertyChanged(nameof(HasLastResponse));
                }
            }
        }

        /// <summary>
        /// Gets or sets the response status message
        /// </summary>
        public string ResponseStatus
        {
            get => _responseStatus;
            set => SetProperty(ref _responseStatus, value);
        }

        /// <summary>
        /// Gets or sets the response execution time message
        /// </summary>
        public string ResponseTime
        {
            get => _responseTime;
            set => SetProperty(ref _responseTime, value);
        }

        /// <summary>
        /// Changes the current Modbus data type used for formatting
        /// </summary>
        public int SelectedDataType
        {
            get => (int)_selectedDataType;
            set
            {
                if (value >= 0 && value < Enum.GetValues(typeof(ModbusDataType)).Length)
                {
                    ModbusDataType newType = (ModbusDataType)value;
                    if (SetProperty(ref _selectedDataType, newType))
                    {
                        // Reformat response data with the new type
                        if (_lastResponse != null)
                        {
                            ReformatResponseData(_lastResponse.Data);
                        }

                        // Update write data inputs when data type changes
                        if (CurrentRequest.IsWriteFunction)
                        {
                            UpdateWriteDataInputs();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether the current function is a write function
        /// </summary>
        public bool IsWriteFunction => CurrentRequest?.IsWriteFunction ?? false;

        /// <summary>
        /// Gets whether the current function is a multiple write function (WriteMultipleCoils or WriteMultipleRegisters)
        /// </summary>
        public bool IsMultipleWriteFunction => CurrentRequest?.FunctionCode == ModbusFunctionCode.WriteMultipleCoils || CurrentRequest?.FunctionCode == ModbusFunctionCode.WriteMultipleRegisters;

        /// <summary>
        /// Collection of input fields for write operations
        /// </summary>
        public ObservableCollection<WriteDataItemViewModel> WriteDataInputs => _writeDataInputs;

        /// <summary>
        /// Gets the available data types based on the current Modbus function
        /// </summary>
        public List<ModbusDataType> AvailableDataTypes => _availableDataTypes;

        /// <summary>
        /// Gets whether a response is available
        /// </summary>
        public bool HasLastResponse => ResponseItems != null && ResponseItems.Count > 0;

        /// <summary>
        /// Gets the response items collection for data binding
        /// </summary>
        public ObservableCollection<ModbusResponseItem> ResponseItems { get; } = new ObservableCollection<ModbusResponseItem>();

        /// <summary>
        /// Gets the available COM ports
        /// </summary>
        public string[] ComPorts => _masterService.GetAvailableComPorts();

        /// <summary>
        /// Gets the standard baud rates
        /// </summary>
        public int[] StandardBaudRates => _masterService.GetStandardBaudRates();

        /// <summary>
        /// Gets the communications log events
        /// </summary>
        public ObservableCollection<CommunicationEvent> CommunicationEvents { get; } = new ObservableCollection<CommunicationEvent>();

        /// <summary>
        /// Gets the register definitions for slave mode
        /// </summary>
        public ObservableCollection<RegisterDefinition> RegisterDefinitions => _slaveService.RegisterDefinitions;

        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Create services
            _masterService = new ModbusMasterService();
            _slaveService = new ModbusSlaveService();

            // Subscribe to events
            _masterService.CommunicationEventOccurred += OnCommunicationEvent;
            _slaveService.CommunicationEventOccurred += OnCommunicationEvent;

            // Set default connection parameters
            _connectionParameters = new TcpConnectionParameters();

            // Create default request parameters
            _currentRequest = new ReadFunctionParameters
            {
                FunctionCode = ModbusFunctionCode.ReadHoldingRegisters,
                SlaveId = 1,
                StartAddress = 0,
                Quantity = 10
            };

            // Initialize commands
            ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(async _ => await DisconnectAsync(), _ => IsConnected);
            SendRequestCommand = new RelayCommand(async _ => await SendRequestAsync(), _ => IsConnected && IsMasterMode);
            ClearEventsCommand = new RelayCommand(_ => ClearEvents());
            ExportEventsCommand = new RelayCommand(_ => ExportEvents(), _ => CommunicationEvents.Count > 0);
            SaveConnectionCommand = new RelayCommand(_ => SaveConnection());
            LoadConnectionCommand = new RelayCommand(_ => LoadConnection());
        }

        /// <summary>
        /// Handle communication events from the services
        /// </summary>
        private void OnCommunicationEvent(object? sender, CommunicationEvent e)
        {
            // Add to UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                CommunicationEvents.Add(e);
            });
        }

        /// <summary>
        /// Connect to a Modbus device
        /// </summary>
        private async Task ConnectAsync()
        {
            try
            {
                // Select the appropriate service based on mode
                _currentService = IsMasterMode ? _masterService : _slaveService;

                // Connect using the current parameters
                IsConnected = await _currentService.ConnectAsync(ConnectionParameters);

                // Update commands
                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                SendRequestCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Connection error: {ex.Message}"));
                IsConnected = false;
            }
        }

        /// <summary>
        /// Disconnect from a Modbus device
        /// </summary>
        private async Task DisconnectAsync()
        {
            try
            {
                if (_currentService != null)
                {
                    await _currentService.DisconnectAsync();
                }

                IsConnected = false;

                // Update commands
                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                SendRequestCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Disconnect error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Send a Modbus request
        /// </summary>
        private async Task SendRequestAsync()
        {
            try
            {
                if (_currentService == null || !_currentService.IsMaster)
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent("Cannot send requests in slave mode"));
                    return;
                }

                // For write operations, parse the write data inputs and set them in the request parameters
                if (CurrentRequest.IsWriteFunction && WriteDataInputs.Count > 0)
                {
                    try
                    {
                        // Handle different write function types
                        if (CurrentRequest is WriteSingleCoilParameters singleCoilParams)
                        {
                            // For coil operations, only the first input is used
                            var firstItem = WriteDataInputs[0];
                            if (!string.IsNullOrWhiteSpace(firstItem.Value))
                            {
                                string val = firstItem.Value;
                                bool boolValue = false;
                                int intValue = 0;
                                bool isValid = bool.TryParse(val, out boolValue) ||
                                             int.TryParse(val, out intValue);

                                if (isValid)
                                {
                                    singleCoilParams.Value = boolValue || intValue != 0;
                                }
                                else
                                {
                                    throw new FormatException("Value must be true/false or 0/1 for coil operations");
                                }
                            }
                        }
                        else if (CurrentRequest is WriteSingleRegisterParameters singleRegParams)
                        {
                            // For single register operations, only the first input is used
                            var firstItem = WriteDataInputs[0];
                            if (!string.IsNullOrWhiteSpace(firstItem.Value))
                            {
                                // Parse based on the selected data type
                                ParseValueForSingleRegister(firstItem.Value, firstItem.SelectedDataType, singleRegParams);
                            }
                        }
                        else if (CurrentRequest is WriteMultipleCoilsParameters multiCoilParams)
                        {
                            // Each input field represents one coil
                            multiCoilParams.Values.Clear();

                            foreach (var item in WriteDataInputs.Where(v => !string.IsNullOrWhiteSpace(v.Value)))
                            {
                                if (bool.TryParse(item.Value, out bool boolValue))
                                {
                                    multiCoilParams.Values.Add(boolValue);
                                }
                                else if (int.TryParse(item.Value, out int intValue))
                                {
                                    multiCoilParams.Values.Add(intValue != 0);
                                }
                                else
                                {
                                    throw new FormatException($"Invalid boolean value: {item.Value}");
                                }
                            }
                        }
                        else if (CurrentRequest is WriteMultipleRegistersParameters multiRegParams)
                        {
                            // Handle multiple registers with potentially different data types per item
                            multiRegParams.Values.Clear();

                            foreach (var item in WriteDataInputs.Where(v => !string.IsNullOrWhiteSpace(v.Value)))
                            {
                                ParseValueForMultipleRegisters(item.Value, item.SelectedDataType, multiRegParams);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Error parsing write values: {ex.Message}"));
                        return;
                    }
                }

                // Execute the request
                var startTime = DateTime.Now;
                var responseObj = await _currentService.ExecuteRequestAsync(CurrentRequest);
                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;

                // Convert the response to ModbusResponseInfo if it came from the master service
                ModbusResponseInfo? response = null;

                if (responseObj is ModbusResponseInfo responseInfo)
                {
                    // Already a ModbusResponseInfo
                    response = responseInfo;
                }
                else if (responseObj != null)
                {
                    // Create a ModbusResponseInfo from the raw data
                    response = new ModbusResponseInfo
                    {
                        IsSuccess = true,
                        Data = responseObj,
                        ExecutionTimeMs = (int)executionTime
                    };
                }

                LastResponse = response; // This will trigger the LastResponse setter which updates status and items

                // Add a received event if successful
                if (response != null && response.IsSuccess)
                {
                    // Log the response data based on its type
                    if (response.Data is bool[] bools)
                    {
                        CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Received {bools.Length} boolean values"));
                    }
                    else if (response.Data is ushort[] registers)
                    {
                        CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Received {registers.Length} register values"));
                    }
                }
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Request error: {ex.Message}"));
                // Create an error response
                LastResponse = new ModbusResponseInfo
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = 0
                };
            }
        }

        /// <summary>
        /// Clear the event log
        /// </summary>
        private void ClearEvents()
        {
            CommunicationEvents.Clear();
            ExportEventsCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Export the event log to a file
        /// </summary>
        private void ExportEvents()
        {
            // Implementation would use a file dialog and save the events
            CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent("Export log functionality not yet implemented"));
        }

        /// <summary>
        /// Save connection parameters to a profile
        /// </summary>
        private void SaveConnection()
        {
            // Implementation would use a file dialog and save the connection parameters
            CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent("Save connection profile functionality not yet implemented"));
        }

        /// <summary>
        /// Load connection parameters from a profile
        /// </summary>
        private void LoadConnection()
        {
            // Implementation would use a file dialog and load the connection parameters
            CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent("Load connection profile functionality not yet implemented"));
        }

        /// <summary>
        /// Change the connection type
        /// </summary>
        public void ChangeConnectionType(ConnectionType type)
        {
            if ((ConnectionParameters?.Type ?? ConnectionType.TCP) != type)
            {
                // Create new connection parameters of the selected type
                ConnectionParameters = type == ConnectionType.TCP
                    ? new TcpConnectionParameters { IsMaster = IsMasterMode }
                    : new RtuConnectionParameters { IsMaster = IsMasterMode };

                OnPropertyChanged(nameof(ConnectionParameters));
            }
        }

        /// <summary>
        /// Updates the list of available data types based on the current function
        /// </summary>
        private void UpdateAvailableDataTypes()
        {
            _availableDataTypes.Clear();

            if (CurrentRequest.IsCoilFunction)
            {
                // For coil functions, only boolean (treated specially in UI)
                _availableDataTypes.Add(ModbusDataType.Binary); // Use Binary to represent boolean
            }
            else
            {
                // For register functions, all data types are available
                _availableDataTypes.Add(ModbusDataType.UInt16);
                _availableDataTypes.Add(ModbusDataType.Int16);
                _availableDataTypes.Add(ModbusDataType.UInt32);
                _availableDataTypes.Add(ModbusDataType.Int32);
                _availableDataTypes.Add(ModbusDataType.Float32);
                _availableDataTypes.Add(ModbusDataType.Float64);
                _availableDataTypes.Add(ModbusDataType.Hex);
                _availableDataTypes.Add(ModbusDataType.Binary);
                _availableDataTypes.Add(ModbusDataType.AsciiString);
            }

            // Notify UI
            OnPropertyChanged(nameof(AvailableDataTypes));
        }

        /// <summary>
        /// Adds a new write data item to the collection
        /// </summary>
        private void AddWriteDataItem()
        {
            bool isCoilWrite = CurrentRequest.FunctionCode == ModbusFunctionCode.WriteSingleCoil || CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleCoils;
            var newItem = new WriteDataItemViewModel(isCoilWrite);
            newItem.OnDataTypeChanged += WriteDataItem_DataTypeChanged;
            _writeDataInputs.Add(newItem);

            CalculateWriteQuantity();

            // Force command availability to update
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Removes a write data item from the collection
        /// </summary>
        private void RemoveWriteDataItem(WriteDataItemViewModel item)
        {
            if (item != null && _writeDataInputs.Count > 1)
            {
                item.OnDataTypeChanged -= WriteDataItem_DataTypeChanged;
                _writeDataInputs.Remove(item);

                CalculateWriteQuantity();

                // Force command availability to update
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Event handler for when a write data item's data type changes
        /// </summary>
        private void WriteDataItem_DataTypeChanged(object sender, EventArgs e)
        {
            CalculateWriteQuantity();
        }

        /// <summary>
        /// Calculates the quantity based on the number of write data items and their data types
        /// </summary>
        private void CalculateWriteQuantity()
        {
            // Null check for CurrentRequest
            if (CurrentRequest == null) return;

            if (CurrentRequest.IsWriteFunction && _writeDataInputs.Count > 0)
            {
                // For coils, each item is 1 coil
                if (CurrentRequest.FunctionCode == ModbusFunctionCode.WriteSingleCoil || CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleCoils)
                {
                    CurrentRequest.Quantity = (ushort)_writeDataInputs.Count;
                    return;
                }

                // For registers, calculate based on data types
                if (CurrentRequest.FunctionCode == ModbusFunctionCode.WriteSingleRegister || CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleRegisters)
                {
                    int totalRegisters = 0;
                    foreach (var item in _writeDataInputs)
                    {
                        totalRegisters += item.GetRegisterCount();
                    }

                    CurrentRequest.Quantity = (ushort)totalRegisters;
                }
            }
        }

        /// <summary>
        /// Updates available data types for each write data item based on function code
        /// </summary>
        private void UpdateAvailableWriteDataTypes()
        {
            // Null check for CurrentRequest
            if (CurrentRequest == null) return;

            bool isCoilWrite = CurrentRequest.FunctionCode == ModbusFunctionCode.WriteSingleCoil || CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleCoils;

            foreach (var item in _writeDataInputs)
            {
                item.UpdateAvailableDataTypes(isCoilWrite);
            }
        }

        /// <summary>
        /// Updates the write data inputs based on quantity and data type
        /// </summary>
        private void UpdateWriteDataInputs()
        {
            try
            {
                // Remove event handlers from existing items
                foreach (var item in _writeDataInputs)
                {
                    item.OnDataTypeChanged -= WriteDataItem_DataTypeChanged;
                }

                _writeDataInputs.Clear();

                // Only add input fields if we're in a write function
                if (CurrentRequest != null && CurrentRequest.IsWriteFunction)
                {
                    bool isCoilWrite = CurrentRequest.FunctionCode == ModbusFunctionCode.WriteSingleCoil || CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleCoils;
                    bool isMultipleWrite = CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleCoils || CurrentRequest.FunctionCode == ModbusFunctionCode.WriteMultipleRegisters;

                    // Add at least one write data item
                    var firstItem = new WriteDataItemViewModel(isCoilWrite);
                    firstItem.OnDataTypeChanged += WriteDataItem_DataTypeChanged;
                    _writeDataInputs.Add(firstItem);

                    // For single write functions, quantity is always 1
                    if (!isMultipleWrite)
                    {
                        CurrentRequest.Quantity = 1;
                    }

                    // Update available data types and calculate quantity
                    UpdateAvailableWriteDataTypes();
                    CalculateWriteQuantity();
                }

                // Notify UI
                OnPropertyChanged(nameof(WriteDataInputs));
                OnPropertyChanged(nameof(IsMultipleWriteFunction));
                OnPropertyChanged(nameof(IsWriteFunction));
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                // Log the exception to help with debugging
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Error updating write data inputs: {ex.Message}"));
            }
        }

        /// <summary>
        /// Parses a value for a single register write operation based on data type
        /// </summary>
        private void ParseValueForSingleRegister(string value, ModbusDataType dataType, WriteSingleRegisterParameters parameters)
        {
            switch (dataType)
            {
                case ModbusDataType.UInt16:
                    if (ushort.TryParse(value, out ushort uintValue))
                    {
                        parameters.Value = uintValue;
                    }
                    else
                    {
                        throw new FormatException($"Invalid UInt16 value: {value}");
                    }
                    break;

                case ModbusDataType.Int16:
                    if (short.TryParse(value, out short intValue))
                    {
                        // Convert signed to register value
                        parameters.Value = unchecked((ushort)intValue);
                    }
                    else
                    {
                        throw new FormatException($"Invalid Int16 value: {value}");
                    }
                    break;

                case ModbusDataType.Binary:
                    // Treat as binary string (1010...) and convert to int
                    try
                    {
                        // Remove any spaces or prefixes
                        string cleanValue = value.Replace(" ", "").Replace("0b", "").Replace("b", "");
                        parameters.Value = Convert.ToUInt16(cleanValue, 2);
                    }
                    catch
                    {
                        throw new FormatException($"Invalid binary value: {value}");
                    }
                    break;

                case ModbusDataType.Hex:
                    // Treat as hex string and convert to int
                    try
                    {
                        // Remove any spaces or prefixes
                        string cleanValue = value.Replace(" ", "").Replace("0x", "").Replace("#", "");
                        parameters.Value = Convert.ToUInt16(cleanValue, 16);
                    }
                    catch
                    {
                        throw new FormatException($"Invalid hexadecimal value: {value}");
                    }
                    break;

                case ModbusDataType.AsciiString:
                    // Take first two characters of string (or pad with nulls)
                    if (value.Length > 0)
                    {
                        char char1 = value.Length > 0 ? value[0] : '\0';
                        char char2 = value.Length > 1 ? value[1] : '\0';
                        parameters.Value = (ushort)((char1 << 8) | char2);
                    }
                    else
                    {
                        parameters.Value = 0;
                    }
                    break;

                default:
                    // For other types (Float32, Float64, etc.) that don't fit in a single register
                    // Just parse as UInt16 as a fallback
                    if (ushort.TryParse(value, out ushort defaultValue))
                    {
                        parameters.Value = defaultValue;
                    }
                    else
                    {
                        throw new FormatException($"Value must be a 16-bit unsigned integer (0-65535) for this operation");
                    }
                    break;
            }
        }

        /// <summary>
        /// Parses a value for multiple register write operations based on data type
        /// </summary>
        private void ParseValueForMultipleRegisters(string value, ModbusDataType dataType, WriteMultipleRegistersParameters parameters)
        {
            switch (dataType)
            {
                case ModbusDataType.UInt16:
                    if (ushort.TryParse(value, out ushort uintValue))
                    {
                        parameters.Values.Add(uintValue);
                    }
                    else
                    {
                        throw new FormatException($"Invalid UInt16 value: {value}");
                    }
                    break;

                case ModbusDataType.Int16:
                    if (short.TryParse(value, out short intValue))
                    {
                        // Convert signed to register value
                        parameters.Values.Add(unchecked((ushort)intValue));
                    }
                    else
                    {
                        throw new FormatException($"Invalid Int16 value: {value}");
                    }
                    break;

                case ModbusDataType.UInt32:
                    if (uint.TryParse(value, out uint uint32Value))
                    {
                        // Split into high and low words
                        ushort highWord = (ushort)(uint32Value >> 16);
                        ushort lowWord = (ushort)(uint32Value & 0xFFFF);
                        parameters.Values.Add(lowWord);  // Low word first (little-endian)
                        parameters.Values.Add(highWord); // High word second
                    }
                    else
                    {
                        throw new FormatException($"Invalid UInt32 value: {value}");
                    }
                    break;

                case ModbusDataType.Int32:
                    if (int.TryParse(value, out int int32Value))
                    {
                        // Split into high and low words
                        ushort highWord = (ushort)((uint)int32Value >> 16);
                        ushort lowWord = (ushort)(int32Value & 0xFFFF);
                        parameters.Values.Add(lowWord);  // Low word first (little-endian)
                        parameters.Values.Add(highWord); // High word second
                    }
                    else
                    {
                        throw new FormatException($"Invalid Int32 value: {value}");
                    }
                    break;

                case ModbusDataType.Float32:
                    if (float.TryParse(value, out float floatValue))
                    {
                        byte[] bytes = BitConverter.GetBytes(floatValue);
                        ushort reg1 = BitConverter.ToUInt16(bytes, 0);
                        ushort reg2 = BitConverter.ToUInt16(bytes, 2);
                        parameters.Values.Add(reg1);
                        parameters.Values.Add(reg2);
                    }
                    else
                    {
                        throw new FormatException($"Invalid Float32 value: {value}");
                    }
                    break;

                case ModbusDataType.Float64:
                    if (double.TryParse(value, out double doubleValue))
                    {
                        byte[] bytes = BitConverter.GetBytes(doubleValue);
                        // Split into 4 registers
                        for (int i = 0; i < 8; i += 2)
                        {
                            ushort regVal = BitConverter.ToUInt16(bytes, i);
                            parameters.Values.Add(regVal);
                        }
                    }
                    else
                    {
                        throw new FormatException($"Invalid Float64 value: {value}");
                    }
                    break;

                case ModbusDataType.Binary:
                    // Treat as binary string (1010...) and convert to int
                    try
                    {
                        // Remove any spaces or prefixes
                        string cleanValue = value.Replace(" ", "").Replace("0b", "").Replace("b", "");
                        parameters.Values.Add(Convert.ToUInt16(cleanValue, 2));
                    }
                    catch
                    {
                        throw new FormatException($"Invalid binary value: {value}");
                    }
                    break;

                case ModbusDataType.Hex:
                    // Treat as hex string and convert to int
                    try
                    {
                        // Remove any spaces or prefixes
                        string cleanValue = value.Replace(" ", "").Replace("0x", "").Replace("#", "");
                        parameters.Values.Add(Convert.ToUInt16(cleanValue, 16));
                    }
                    catch
                    {
                        throw new FormatException($"Invalid hexadecimal value: {value}");
                    }
                    break;

                case ModbusDataType.AsciiString:
                    // Convert string to registers (2 chars per register)
                    for (int i = 0; i < value.Length; i += 2)
                    {
                        char char1 = i < value.Length ? value[i] : '\0';
                        char char2 = i + 1 < value.Length ? value[i + 1] : '\0';
                        ushort regVal = (ushort)((char1 << 8) | char2);
                        parameters.Values.Add(regVal);
                    }
                    break;
            }
        }

        /// <summary>
        /// Reformats response data based on the selected data type
        /// </summary>
        private void ReformatResponseData(object data)
        {
            // Clear existing response items
            ResponseItems.Clear();

            // Format based on data type
            if (data is ushort[] registers)
            {
                var startAddress = CurrentRequest?.StartAddress ?? 0;

                ModbusDataType dataType = (ModbusDataType)SelectedDataType;
                switch (dataType)
                {
                    case ModbusDataType.UInt16:
                        // Standard unsigned 16-bit integers
                        for (int i = 0; i < registers.Length; i++)
                        {
                            ResponseItems.Add(new ModbusResponseItem
                            {
                                Address = startAddress + i,
                                Value = registers[i]
                            });
                        }
                        break;

                    case ModbusDataType.Int16:
                        // Signed 16-bit integers
                        for (int i = 0; i < registers.Length; i++)
                        {
                            ResponseItems.Add(new ModbusResponseItem
                            {
                                Address = startAddress + i,
                                Value = (short)registers[i]
                            });
                        }
                        break;

                    case ModbusDataType.UInt32:
                        // 32-bit unsigned integers (2 registers per value)
                        for (int i = 0; i < registers.Length - 1; i += 2)
                        {
                            if (i + 1 < registers.Length)
                            {
                                uint value = (uint)((registers[i] << 16) | registers[i + 1]);
                                ResponseItems.Add(new ModbusResponseItem
                                {
                                    Address = startAddress + i,
                                    Value = value
                                });
                            }
                        }
                        break;

                    case ModbusDataType.Int32:
                        // 32-bit signed integers (2 registers per value)
                        for (int i = 0; i < registers.Length - 1; i += 2)
                        {
                            if (i + 1 < registers.Length)
                            {
                                int value = (registers[i] << 16) | registers[i + 1];
                                ResponseItems.Add(new ModbusResponseItem
                                {
                                    Address = startAddress + i,
                                    Value = value
                                });
                            }
                        }
                        break;

                    case ModbusDataType.Float32:
                        // 32-bit floating point (2 registers per value)
                        for (int i = 0; i < registers.Length - 1; i += 2)
                        {
                            if (i + 1 < registers.Length)
                            {
                                // Convert two 16-bit registers to a 32-bit integer
                                uint intValue = (uint)((registers[i] << 16) | registers[i + 1]);

                                // Reinterpret the 32-bit integer as a float
                                float floatValue = BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);

                                ResponseItems.Add(new ModbusResponseItem
                                {
                                    Address = startAddress + i,
                                    Value = floatValue
                                });
                            }
                        }
                        break;

                    case ModbusDataType.Float64:
                        // 64-bit floating point (4 registers per value)
                        for (int i = 0; i < registers.Length - 3; i += 4)
                        {
                            if (i + 3 < registers.Length)
                            {
                                // Convert four 16-bit registers to a 64-bit long
                                ulong longValue = (ulong)registers[i] << 48 |
                                                 (ulong)registers[i + 1] << 32 |
                                                 (ulong)registers[i + 2] << 16 |
                                                 registers[i + 3];

                                // Reinterpret the 64-bit long as a double
                                double doubleValue = BitConverter.ToDouble(BitConverter.GetBytes(longValue), 0);

                                ResponseItems.Add(new ModbusResponseItem
                                {
                                    Address = startAddress + i,
                                    Value = doubleValue
                                });
                            }
                        }
                        break;

                    case ModbusDataType.AsciiString:
                        // ASCII string (2 chars per register)
                        var chars = new List<char>();
                        foreach (var register in registers)
                        {
                            // Each register contains 2 ASCII characters
                            chars.Add((char)(register >> 8));    // High byte
                            chars.Add((char)(register & 0xFF));  // Low byte
                        }

                        // Create a single string item from all registers
                        string asciiValue = new string(chars.ToArray()).TrimEnd('\0');
                        ResponseItems.Add(new ModbusResponseItem
                        {
                            Address = startAddress,
                            Value = asciiValue
                        });
                        break;
                }
            }
            else if (data is bool[] coils)
            {
                // For boolean values, there's only one format
                var startAddress = CurrentRequest?.StartAddress ?? 0;
                for (int i = 0; i < coils.Length; i++)
                {
                    ResponseItems.Add(new ModbusResponseItem
                    {
                        Address = startAddress + i,
                        Value = coils[i]
                    });
                }
            }

            // Notify UI that HasLastResponse has changed
            OnPropertyChanged(nameof(HasLastResponse));
        }

        /// <summary>
        /// Update a register definition in slave mode
        /// </summary>
        public void UpdateRegister(RegisterDefinition register)
        {
            if (!IsMasterMode && _currentService == _slaveService)
            {
                _slaveService.UpdateRegisterValue(register);
            }
        }

        /// <summary>
        /// Add a register definition in slave mode
        /// </summary>
        public void AddRegister(RegisterDefinition register)
        {
            if (!IsMasterMode && _currentService == _slaveService)
            {
                try
                {
                    _slaveService.AddRegister(register);
                    CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Added register at address {register.Address}"));
                }
                catch (Exception ex)
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Failed to add register: {ex.Message}"));
                }
            }
        }

        /// <summary>
        /// Remove a register definition in slave mode
        /// </summary>
        public void RemoveRegister(RegisterDefinition register)
        {
            if (!IsMasterMode && _currentService == _slaveService)
            {
                _slaveService.RemoveRegister(register);
                CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Removed register at address {register.Address}"));
            }
        }

        /// <summary>
        /// Create a new read function request based on the selected function code
        /// </summary>
        public void CreateReadRequest(ModbusFunctionCode functionCode)
        {
            try
            {
                // Get the current quantity value without unsafe casting
                ushort quantity = 10; // Default quantity
                
                // If we have an existing request, try to use its quantity
                if (CurrentRequest != null)
                {
                    // Access the quantity directly without casting
                    quantity = CurrentRequest.Quantity;
                }

                // Create the new read request with safe parameters
                CurrentRequest = new ReadFunctionParameters
                {
                    FunctionCode = functionCode,
                    SlaveId = CurrentRequest?.SlaveId ?? 1,
                    StartAddress = CurrentRequest?.StartAddress ?? 0,
                    Quantity = quantity
                };
                
                // Ensure UI is updated
                OnPropertyChanged(nameof(IsWriteFunction));
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Error creating read request: {ex.Message}"));
            }
        }

        /// <summary>
        /// Create a new write single coil request
        /// </summary>
        public void CreateWriteSingleCoilRequest()
        {
            CurrentRequest = new WriteSingleCoilParameters
            {
                SlaveId = CurrentRequest?.SlaveId ?? 1,
                StartAddress = CurrentRequest?.StartAddress ?? 0
            };
        }

        /// <summary>
        /// Create a new write single register request
        /// </summary>
        public void CreateWriteSingleRegisterRequest()
        {
            CurrentRequest = new WriteSingleRegisterParameters
            {
                SlaveId = CurrentRequest?.SlaveId ?? 1,
                StartAddress = CurrentRequest?.StartAddress ?? 0
            };
        }

        /// <summary>
        /// Create a new write multiple coils request
        /// </summary>
        public void CreateWriteMultipleCoilsRequest()
        {
            var request = new WriteMultipleCoilsParameters
            {
                SlaveId = CurrentRequest?.SlaveId ?? 1,
                StartAddress = CurrentRequest?.StartAddress ?? 0
            };

            // Add some default values
            request.Values.Add(false);
            request.Values.Add(false);

            CurrentRequest = request;
        }

        /// <summary>
        /// Create a new write multiple registers request
        /// </summary>
        public void CreateWriteMultipleRegistersRequest()
        {
            var request = new WriteMultipleRegistersParameters
            {
                SlaveId = CurrentRequest?.SlaveId ?? 1,
                StartAddress = CurrentRequest?.StartAddress ?? 0
            };

            // Add some default values
            request.Values.Add(0);
            request.Values.Add(0);

            CurrentRequest = request;
        }
    }
}
