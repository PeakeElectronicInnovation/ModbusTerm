using Microsoft.Win32;
using ModbusTerm.Helpers;
using ModbusTerm.Models;
using ModbusTerm.Services;
using ModbusTerm.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ModbusTerm.ViewModels
{
    /// <summary>
    /// Main ViewModel for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private const string PROFILE_FILE_EXTENSION = "json";
        private const string PROFILE_FILE_FILTER = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
        private readonly IModbusService _masterService;
        private readonly ModbusSlaveService _slaveService;
        private readonly ProfileService _profileService;
        private IModbusService? _currentService;
        private ConnectionParameters _connectionParameters;
        private bool _isConnected;
        private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
        private ObservableCollection<string> _profiles = new ObservableCollection<string>();
        private string _selectedProfileName = "Default Profile";
        private bool _isMasterMode = true;
        private ModbusFunctionParameters _currentRequest;
        private ModbusResponseInfo? _lastResponse;
        private ObservableCollection<ModbusResponseItem> _responseItems = new ObservableCollection<ModbusResponseItem>();
        private string _responseStatus = "-";
        private string _responseTime = "- ms";
        private bool _isDeviceScanActive = false;
        private ObservableCollection<DeviceScanResult> _deviceScanResults = new ObservableCollection<DeviceScanResult>();
        private CancellationTokenSource? _deviceScanCts;
        private ModbusDataType _selectedDataType = ModbusDataType.UInt16;
        private bool _autoScrollEventLog = true;
        private ObservableCollection<WriteDataItemViewModel> _writeDataInputs = new ObservableCollection<WriteDataItemViewModel>();
        private List<ModbusDataType> _availableDataTypes = new List<ModbusDataType>();
        private ICommand _addWriteDataItemCommand;
        private ICommand _removeWriteDataItemCommand;
        private ICommand _removeLastWriteDataItemCommand;
        private RegisterDefinition? _selectedRegister;
        private ICommand _addRegisterCommand;
        private ICommand _removeRegisterCommand;
        private ICommand _clearRegistersCommand;
        private ICommand _editRegisterCommand;
        private ICommand _importRegistersCommand;
        private ICommand _exportRegistersCommand;

        // Input register management
        private RegisterDefinition? _selectedInputRegister;
        private ICommand _addInputRegisterCommand;
        private ICommand _removeInputRegisterCommand;
        private ICommand _clearInputRegistersCommand;
        private ICommand _editInputRegisterCommand;
        private ICommand _importInputRegistersCommand;
        private ICommand _exportInputRegistersCommand;
        private bool _showInputRegisters = false;
        
        // Coils management
        private BooleanRegisterDefinition? _selectedCoil;
        private ICommand _addCoilCommand;
        private ICommand _removeCoilCommand;
        private ICommand _importCoilsCommand;
        private ICommand _exportCoilsCommand;
        private bool _showCoils = true;
        
        // Discrete Inputs management
        private BooleanRegisterDefinition? _selectedDiscreteInput;
        private ICommand _addDiscreteInputCommand;
        private ICommand _removeDiscreteInputCommand;
        private ICommand _importDiscreteInputsCommand;
        private ICommand _exportDiscreteInputsCommand;
        private bool _showDiscreteInputs = true;

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
        /// Command to start scanning for Modbus devices
        /// </summary>
        public RelayCommand ScanForDevicesCommand { get; }

        /// <summary>
        /// Command to stop an ongoing device scan
        /// </summary>
        public RelayCommand StopDeviceScanCommand { get; }

        /// <summary>
        /// Command to export the event log
        /// </summary>
        public RelayCommand ExportEventsCommand { get; }

        /// <summary>
        /// Command to save connection parameters
        /// </summary>
        public RelayCommand SaveConnectionCommand { get; }

        /// <summary>
        /// Command to remove the selected profile
        /// </summary>
        public RelayCommand RemoveProfileCommand { get; }

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
            execute: parameter => { if (parameter is WriteDataItemViewModel item) RemoveWriteDataItem(item); },
            canExecute: parameter => IsMultipleWriteFunction && _writeDataInputs.Count > 1);

        /// <summary>
        /// Gets the command to remove the last write data item
        /// </summary>
        public ICommand RemoveLastWriteDataItemCommand => _removeLastWriteDataItemCommand ??= new RelayCommand(
            execute: _ => RemoveLastWriteDataItem(),
            canExecute: _ => CanRemoveLastWriteDataItem());
            
        /// <summary>
        /// Gets or sets the selected register in slave mode
        /// </summary>
        public RegisterDefinition? SelectedRegister
        {
            get => _selectedRegister;
            set
            {
                if (SetProperty(ref _selectedRegister, value))
                {
                    OnPropertyChanged(nameof(HasSelectedRegister));
                }
            }
        }
        
        /// <summary>
        /// Gets whether there is a selected register
        /// </summary>
        public bool HasSelectedRegister => SelectedRegister != null;
        
        /// <summary>
        /// Gets whether there are holding registers defined
        /// </summary>
        public bool HasRegisters => _slaveService.RegisterDefinitions.Count > 0;

        /// <summary>
        /// Gets whether there are input registers defined
        /// </summary>
        public bool HasInputRegisters => _slaveService.InputRegisterDefinitions.Count > 0;
        
        /// <summary>
        /// Gets the command to add a new holding register in slave mode
        /// </summary>
        public ICommand AddRegisterCommand => _addRegisterCommand;
        
        /// <summary>
        /// Gets the command to remove a holding register in slave mode
        /// </summary>
        public ICommand RemoveRegisterCommand => _removeRegisterCommand;
        
        /// <summary>
        /// Gets the command to clear all holding registers in slave mode
        /// </summary>
        public ICommand ClearRegistersCommand => _clearRegistersCommand;
        
        /// <summary>
        /// Gets the command to edit a holding register in slave mode
        /// </summary>
        public ICommand EditRegisterCommand => _editRegisterCommand;
        
        /// <summary>
        /// Gets the command to import holding registers from a file
        /// </summary>
        public ICommand ImportRegistersCommand => _importRegistersCommand;
        
        /// <summary>
        /// Gets the command to export holding registers to a file
        /// </summary>
        public ICommand ExportRegistersCommand => _exportRegistersCommand;

        /// <summary>
        /// Gets or sets the selected input register in slave mode
        /// </summary>
        public RegisterDefinition? SelectedInputRegister
        {
            get => _selectedInputRegister;
            set
            {
                if (SetProperty(ref _selectedInputRegister, value))
                {
                    OnPropertyChanged(nameof(HasSelectedInputRegister));
                }
            }
        }

        /// <summary>
        /// Gets whether there is a selected input register
        /// </summary>
        public bool HasSelectedInputRegister => SelectedInputRegister != null;

        /// <summary>
        /// Gets or sets whether to show input registers tab
        /// </summary>
        public bool ShowInputRegisters
        {
            get => _showInputRegisters;
            set => SetProperty(ref _showInputRegisters, value);
        }
        
        /// <summary>
        /// Gets or sets whether to show coils tab
        /// </summary>
        public bool ShowCoils
        {
            get => _showCoils;
            set => SetProperty(ref _showCoils, value);
        }
        
        /// <summary>
        /// Gets or sets whether to show discrete inputs tab
        /// </summary>
        public bool ShowDiscreteInputs
        {
            get => _showDiscreteInputs;
            set => SetProperty(ref _showDiscreteInputs, value);
        }

        /// <summary>
        /// Gets the command to add a new input register in slave mode
        /// </summary>
        public ICommand AddInputRegisterCommand => _addInputRegisterCommand;
        
        /// <summary>
        /// Gets the command to remove an input register in slave mode
        /// </summary>
        public ICommand RemoveInputRegisterCommand => _removeInputRegisterCommand;
        
        /// <summary>
        /// Gets the command to clear all input registers in slave mode
        /// </summary>
        public ICommand ClearInputRegistersCommand => _clearInputRegistersCommand;
        
        /// <summary>
        /// Gets the command to edit an input register in slave mode
        /// </summary>
        public ICommand EditInputRegisterCommand => _editInputRegisterCommand;
        
        /// <summary>
        /// Gets the command to import input registers from a file
        /// </summary>
        public ICommand ImportInputRegistersCommand => _importInputRegistersCommand;
        
        /// <summary>
        /// Gets the command to export input registers to a file
        /// </summary>
        public ICommand ExportInputRegistersCommand => _exportInputRegistersCommand;
        
        /// <summary>
        /// Gets or sets the selected coil in slave mode
        /// </summary>
        public BooleanRegisterDefinition? SelectedCoil
        {
            get => _selectedCoil;
            set
            {
                if (SetProperty(ref _selectedCoil, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCoil));
                }
            }
        }
        
        /// <summary>
        /// Gets whether there is a selected coil
        /// </summary>
        public bool HasSelectedCoil => SelectedCoil != null;
        
        /// <summary>
        /// Gets whether there are coils defined
        /// </summary>
        public bool HasCoils => _slaveService.CoilDefinitions.Count > 0;
        
        /// <summary>
        /// Gets the collection of coil definitions from the slave service
        /// </summary>
        public ObservableCollection<BooleanRegisterDefinition> CoilDefinitions => _slaveService.CoilDefinitions;
        
        /// <summary>
        /// Gets the command to add a new coil in slave mode
        /// </summary>
        public ICommand AddCoilCommand => _addCoilCommand;
        
        /// <summary>
        /// Gets the command to remove a coil in slave mode
        /// </summary>
        public ICommand RemoveCoilCommand => _removeCoilCommand;
        
        /// <summary>
        /// Gets the command to import coils from a file
        /// </summary>
        public ICommand ImportCoilsCommand => _importCoilsCommand;
        
        /// <summary>
        /// Gets the command to export coils to a file
        /// </summary>
        public ICommand ExportCoilsCommand => _exportCoilsCommand;
        
        /// <summary>
        /// Gets or sets the selected discrete input in slave mode
        /// </summary>
        public BooleanRegisterDefinition? SelectedDiscreteInput
        {
            get => _selectedDiscreteInput;
            set
            {
                if (SetProperty(ref _selectedDiscreteInput, value))
                {
                    OnPropertyChanged(nameof(HasSelectedDiscreteInput));
                }
            }
        }
        
        /// <summary>
        /// Gets whether there is a selected discrete input
        /// </summary>
        public bool HasSelectedDiscreteInput => SelectedDiscreteInput != null;
        
        /// <summary>
        /// Gets whether there are discrete inputs defined
        /// </summary>
        public bool HasDiscreteInputs => _slaveService.DiscreteInputDefinitions.Count > 0;
        
        /// <summary>
        /// Gets the collection of discrete input definitions from the slave service
        /// </summary>
        public ObservableCollection<BooleanRegisterDefinition> DiscreteInputDefinitions => _slaveService.DiscreteInputDefinitions;
        
        /// <summary>
        /// Gets the command to add a new discrete input in slave mode
        /// </summary>
        public ICommand AddDiscreteInputCommand => _addDiscreteInputCommand;
        
        /// <summary>
        /// Gets the command to remove a discrete input in slave mode
        /// </summary>
        public ICommand RemoveDiscreteInputCommand => _removeDiscreteInputCommand;
        
        /// <summary>
        /// Gets the command to import discrete inputs from a file
        /// </summary>
        public ICommand ImportDiscreteInputsCommand => _importDiscreteInputsCommand;
        
        /// <summary>
        /// Gets the command to export discrete inputs to a file
        /// </summary>
        public ICommand ExportDiscreteInputsCommand => _exportDiscreteInputsCommand;

        /// <summary>
        /// Gets or sets whether the connection is established
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        /// <summary>
        /// Gets or sets the connection status
        /// </summary>
        public ConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
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
            set
            {
                if (SetProperty(ref _connectionParameters, value))
                {
                    // Update selected profile name
                    _selectedProfileName = value?.ProfileName ?? "Default Profile";
                    OnPropertyChanged(nameof(SelectedProfileName));
                    
                    // Update connection type properties for binding
                    OnPropertyChanged(nameof(IsTcpMode));
                    OnPropertyChanged(nameof(IsRtuMode));
                }
            }
        }
        
        /// <summary>
        /// Gets the available profiles
        /// </summary>
        public ObservableCollection<string> Profiles => _profiles;
        
        /// <summary>
        /// Gets or sets the selected profile name
        /// </summary>
        public string SelectedProfileName
        {
            get => _selectedProfileName;
            set
            {
                if (SetProperty(ref _selectedProfileName, value) && value != null)
                {
                    // Load the selected profile
                    LoadProfileAsync(value);
                }
            }
        }

        /// <summary>
        /// Gets the current Modbus function parameters
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
        /// Gets the last Modbus response
        /// </summary>
        public ModbusResponseInfo? LastResponse
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
                        if (_lastResponse != null && _lastResponse.Data != null)
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
        /// Gets or sets whether a device scan is currently in progress
        /// </summary>
        public bool IsDeviceScanActive
        {
            get => _isDeviceScanActive;
            private set 
            { 
                if (SetProperty(ref _isDeviceScanActive, value))
                {
                    // Update UI to reflect scan mode changes
                    OnPropertyChanged(nameof(IsInScanMode));
                }
            }
        }

        /// <summary>
        /// Gets whether we're currently in device scan mode (affects how the response panel is displayed)
        /// </summary>
        public bool IsInScanMode => IsDeviceScanActive || (_deviceScanResults?.Count > 0 && _lastResponse == null);

        /// <summary>
        /// Gets the collection of device scan results
        /// </summary>
        public ObservableCollection<DeviceScanResult> DeviceScanResults => _deviceScanResults;

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
        /// Refreshes the list of available COM ports
        /// </summary>
        public void RefreshComPorts()
        {
            // Notify UI that the ComPorts property has changed
            OnPropertyChanged(nameof(ComPorts));

            // If there's a valid connection parameter of RTU type, check if the current port is still available
            if (_connectionParameters is RtuConnectionParameters rtuParams)
            {
                string[] availablePorts = _masterService.GetAvailableComPorts();

                // Check if the currently selected port is not available anymore
                if (!string.IsNullOrEmpty(rtuParams.ComPort) && !availablePorts.Contains(rtuParams.ComPort))
                {
                    // Select first available port if any exist
                    if (availablePorts.Length > 0)
                    {
                        rtuParams.ComPort = availablePorts[0];
                    }
                    else
                    {
                        rtuParams.ComPort = string.Empty;
                    }

                    // Notify UI that the connection parameters have changed
                    OnPropertyChanged(nameof(ConnectionParameters));
                }
            }
        }

        /// <summary>
        /// Gets the standard baud rates
        /// </summary>
        public int[] StandardBaudRates => _masterService.GetStandardBaudRates();

        /// <summary>
        /// Gets the communications log events
        /// </summary>
        public ObservableCollection<CommunicationEvent> CommunicationEvents { get; } = new ObservableCollection<CommunicationEvent>();

        /// <summary>
        /// Gets the holding register definitions for slave mode
        /// </summary>
        public ObservableCollection<RegisterDefinition> RegisterDefinitions => _slaveService.RegisterDefinitions;

        /// <summary>
        /// Gets the input register definitions for slave mode
        /// </summary>
        public ObservableCollection<RegisterDefinition> InputRegisterDefinitions => _slaveService.InputRegisterDefinitions;

        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Create services
            _masterService = new ModbusMasterService();
            _slaveService = new ModbusSlaveService();
            _profileService = new ProfileService();

            // Subscribe to events
            _masterService.CommunicationEventOccurred += OnCommunicationEvent;
            _slaveService.CommunicationEventOccurred += OnCommunicationEvent;
            
            // Subscribe to device scan events
            _masterService.DeviceScanResultReceived += OnDeviceScanResultReceived;

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
            
            // Initialize connection commands
            ConnectCommand = new RelayCommand(_ => _ = ConnectAsync(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(_ => _ = DisconnectAsync(), _ => IsConnected);
            SendRequestCommand = new RelayCommand(_ => _ = SendRequestAsync(), _ => IsConnected && IsMasterMode);
            ClearEventsCommand = new RelayCommand(_ => CommunicationEvents.Clear());
            ExportEventsCommand = new RelayCommand(_ => ExportEvents());
            SaveConnectionCommand = new RelayCommand(_ => SaveConnection());
            RemoveProfileCommand = new RelayCommand(_ => RemoveProfile(), _ => !string.IsNullOrEmpty(SelectedProfileName) && SelectedProfileName != "Default Profile");
            ScanForDevicesCommand = new RelayCommand(_ => StartDeviceScan(), _ => CanScanForDevices());
            StopDeviceScanCommand = new RelayCommand(_ => StopDeviceScan(), _ => CanStopDeviceScan());
            
            // Load profiles and default profile
            LoadProfilesAsync();
            LoadProfileAsync("Default Profile");
            
            // Initialize write data item commands
            _addWriteDataItemCommand = new RelayCommand(_ => AddWriteDataItem(), _ => IsMultipleWriteFunction);
            _removeWriteDataItemCommand = new RelayCommand(
                parameter => { if (parameter is WriteDataItemViewModel item) RemoveWriteDataItem(item); },
                parameter => IsMultipleWriteFunction && _writeDataInputs.Count > 1);
            _removeLastWriteDataItemCommand = new RelayCommand(_ => RemoveLastWriteDataItem(), _ => CanRemoveLastWriteDataItem());
            
            // Initialize holding register management commands
            _addRegisterCommand = new RelayCommand(_ => AddRegister(), _ => IsSlaveMode);
            _removeRegisterCommand = new RelayCommand(_ => RemoveRegister(), _ => IsSlaveMode && HasSelectedRegister);
            _clearRegistersCommand = new RelayCommand(_ => ClearRegisters(), _ => IsSlaveMode && HasRegisters);
            _editRegisterCommand = new RelayCommand(_ => EditRegister(), _ => IsSlaveMode && HasSelectedRegister);
            _importRegistersCommand = new RelayCommand(_ => ImportRegisters(), _ => IsSlaveMode);
            _exportRegistersCommand = new RelayCommand(_ => ExportRegisters(), _ => IsSlaveMode && HasRegisters);
            
            // Initialize input register management commands
            _addInputRegisterCommand = new RelayCommand(_ => AddInputRegister(), _ => IsSlaveMode);
            _removeInputRegisterCommand = new RelayCommand(_ => RemoveInputRegister(), _ => IsSlaveMode && HasSelectedInputRegister);
            _clearInputRegistersCommand = new RelayCommand(_ => ClearInputRegisters(), _ => IsSlaveMode && HasInputRegisters);
            _editInputRegisterCommand = new RelayCommand(_ => EditInputRegister(), _ => IsSlaveMode && HasSelectedInputRegister);
            _importInputRegistersCommand = new RelayCommand(_ => ImportInputRegisters(), _ => IsSlaveMode);
            _exportInputRegistersCommand = new RelayCommand(_ => ExportInputRegisters(), _ => IsSlaveMode && HasInputRegisters);
            
            // Initialize coil management commands
            _addCoilCommand = new RelayCommand(_ => AddCoil(), _ => IsSlaveMode);
            _removeCoilCommand = new RelayCommand(_ => RemoveCoil(), _ => IsSlaveMode && HasSelectedCoil);
            _importCoilsCommand = new RelayCommand(_ => ImportCoils(), _ => IsSlaveMode);
            _exportCoilsCommand = new RelayCommand(_ => ExportCoils(), _ => IsSlaveMode && HasCoils);
            
            // Initialize discrete input management commands
            _addDiscreteInputCommand = new RelayCommand(_ => AddDiscreteInput(), _ => IsSlaveMode);
            _removeDiscreteInputCommand = new RelayCommand(_ => RemoveDiscreteInput(), _ => IsSlaveMode && HasSelectedDiscreteInput);
            _importDiscreteInputsCommand = new RelayCommand(_ => ImportDiscreteInputs(), _ => IsSlaveMode);
            _exportDiscreteInputsCommand = new RelayCommand(_ => ExportDiscreteInputs(), _ => IsSlaveMode && HasDiscreteInputs);
            
            // Default showing tabs to true
            _showInputRegisters = true;
            _showCoils = true;
            _showDiscreteInputs = true;
            
            // Initialize data types for slave mode registers
            InitializeDataTypes();
            
            // Set up event handler for register value changes
            _slaveService.RegisterDefinitions.CollectionChanged += RegisterDefinitions_CollectionChanged;
            _slaveService.InputRegisterDefinitions.CollectionChanged += InputRegisterDefinitions_CollectionChanged;
            _slaveService.CoilDefinitions.CollectionChanged += CoilDefinitions_CollectionChanged;
            _slaveService.DiscreteInputDefinitions.CollectionChanged += DiscreteInputDefinitions_CollectionChanged;
            
            // Hook up property changed events to existing registers
            foreach (var register in _slaveService.RegisterDefinitions)
            {
                if (register is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += Register_PropertyChanged;
                }
            }
            
            // Hook up property changed events to existing input registers
            foreach (var register in _slaveService.InputRegisterDefinitions)
            {
                if (register is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += InputRegister_PropertyChanged;
                }
            }
            
            // Hook up property changed events to existing coils
            foreach (var coil in _slaveService.CoilDefinitions)
            {
                if (coil is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += Coil_PropertyChanged;
                }
            }
            
            // Hook up property changed events to existing discrete inputs
            foreach (var discreteInput in _slaveService.DiscreteInputDefinitions)
            {
                if (discreteInput is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += DiscreteInput_PropertyChanged;
                }
            }
            
            // Subscribe to external register changes from Modbus master devices
            _slaveService.RegisterChanged += SlaveService_RegisterChanged;
            _slaveService.CoilChanged += SlaveService_CoilChanged;
            
            // Subscribe to the highlight timer tick event
            _highlightTimer.Tick += HighlightTimer_Tick;
        }

        // For tracking timer to clear register highlights
        private readonly DispatcherTimer _highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HIGHLIGHT_DURATION_MS) };
        private HashSet<RegisterDefinition> _highlightedRegisters = new HashSet<RegisterDefinition>();
        private HashSet<BooleanRegisterDefinition> _highlightedCoils = new HashSet<BooleanRegisterDefinition>();
        private HashSet<BooleanRegisterDefinition> _highlightedDiscreteInputs = new HashSet<BooleanRegisterDefinition>();
        private const int HIGHLIGHT_DURATION_MS = 5000; // 5 seconds highlight duration
        
        /// <summary>
        /// Handle external changes to holding registers from a Modbus master
        /// </summary>
        private void SlaveService_RegisterChanged(object? sender, RegisterChangedEventArgs e)
        {
            // Ensure we update the UI on the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Update all registers that were changed
                for (int i = 0; i < e.Values.Length; i++)
                {
                    // Calculate the actual address for this index
                    ushort address = (ushort)(e.StartAddress + i);
                    
                    // Find the register in our collection
                    var register = _slaveService.RegisterDefinitions.FirstOrDefault(r => r.Address == address);
                    if (register != null)
                    {
                        // Update UI with the changed value, but don't trigger another write back
                        register.SuppressNotifications = true;
                        register.Value = e.Values[i];
                        register.SuppressNotifications = false;

                        // Force a UI refresh by notifying change of a UI-bound property
                        register.IsRecentlyModified = true;
                        _highlightedRegisters.Add(register);
                    }
                }
                
                // Log the change
                if (e.Values.Length > 0)
                {
                    var addressRange = e.Values.Length > 1 ? 
                        $"{e.StartAddress}-{e.StartAddress + e.Values.Length - 1}" : 
                        $"{e.StartAddress}";
                        
                    OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent(
                        $"Holding register(s) at addresses {addressRange} modified by external master"));

                    // Start the timer to clear the highlight if it's not running
                    if (!_highlightTimer.IsEnabled)
                    {
                        _highlightTimer.Start();
                    }
                }
            });
        }
        
        /// <summary>
        /// Handle external changes to coils from a Modbus master
        /// </summary>
        private void SlaveService_CoilChanged(object? sender, CoilChangedEventArgs e)
        {
            // Ensure we update the UI on the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Update all coils that were changed
                for (int i = 0; i < e.Values.Length; i++)
                {
                    // Calculate the actual address for this index
                    ushort address = (ushort)(e.StartAddress + i);
                    
                    // Find the coil in our collection
                    var coil = _slaveService.CoilDefinitions.FirstOrDefault(c => c.Address == address);
                    if (coil != null)
                    {
                        // Update UI with the changed value, but don't trigger another write back
                        coil.SuppressNotifications = true;
                        coil.Value = e.Values[i];
                        coil.SuppressNotifications = false;

                        // Force UI refresh by explicitly notifying of property change
                        coil.ForcePropertyChanged(nameof(BooleanRegisterDefinition.Value));
                        coil.IsRecentlyModified = true;
                        _highlightedCoils.Add(coil);
                    }
                }
                
                // Log the change
                if (e.Values.Length > 0)
                {
                    var addressRange = e.Values.Length > 1 ? 
                        $"{e.StartAddress}-{e.StartAddress + e.Values.Length - 1}" : 
                        $"{e.StartAddress}";
                        
                    OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent(
                        $"Coil(s) at addresses {addressRange} modified by external master"));

                    // Start the timer to clear the highlight if it's not running
                    if (!_highlightTimer.IsEnabled)
                    {
                        _highlightTimer.Start();
                    }
                }
            });
        }
        
        /// <summary>
        /// Timer tick handler to clear highlights after delay
        /// </summary>
        /// <summary>
        /// Add a new coil to the slave coil collection
        /// </summary>
        private void AddCoil()
        {
            try
            {
                // Find the next available address
                ushort nextAddress = 0;
                if (_slaveService.CoilDefinitions.Count > 0)
                {
                    var last = _slaveService.CoilDefinitions.OrderBy(c => c.Address).Last();
                    nextAddress = (ushort)(last.Address + 1);
                }
                
                // Create a new coil with default values
                var newCoil = new BooleanRegisterDefinition
                {
                    Address = nextAddress,
                    Value = false,
                    Name = $"Coil {nextAddress}",
                    Description = "New coil",
                    IsRecentlyModified = false
                };
                
                // Temporarily disable coil changed notifications
                bool oldSuppressNotifications = newCoil.SuppressNotifications;
                newCoil.SuppressNotifications = true;
                
                try
                {
                    // Add to the service's collection
                    _slaveService.CoilDefinitions.Add(newCoil);
                    
                    // Hook up property change notification
                    if (newCoil is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += Coil_PropertyChanged;
                    }
                    
                    // Update the service's data store
                    _slaveService.UpdateCoilValue(newCoil);
                    
                    // Select the new coil
                    SelectedCoil = newCoil;
                    
                    // Notify UI that coils collection has changed
                    OnPropertyChanged(nameof(HasCoils));
                }
                finally
                {
                    // Restore the original notification state
                    newCoil.SuppressNotifications = oldSuppressNotifications;
                }
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Added coil at address {nextAddress}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to add coil: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Remove the currently selected coil from the slave coil collection
        /// </summary>
        private void RemoveCoil()
        {
            if (SelectedCoil == null)
            {
                return;
            }
            
            try
            {
                var coilToRemove = SelectedCoil;
                SelectedCoil = null; // Clear selection before removing to avoid UI issues
                
                // Remove from the service's collection
                _slaveService.CoilDefinitions.Remove(coilToRemove);
                
                // Unsubscribe from property changed events
                if (coilToRemove is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged -= Coil_PropertyChanged;
                }
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Removed coil at address {coilToRemove.Address}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to remove coil: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Add a new discrete input to the slave discrete input collection
        /// </summary>
        private void AddDiscreteInput()
        {
            try
            {
                // Find the next available address
                ushort nextAddress = 0;
                if (_slaveService.DiscreteInputDefinitions.Count > 0)
                {
                    var last = _slaveService.DiscreteInputDefinitions.OrderBy(d => d.Address).Last();
                    nextAddress = (ushort)(last.Address + 1);
                }
                
                // Create a new discrete input with default values
                var newDiscreteInput = new BooleanRegisterDefinition
                {
                    Address = nextAddress,
                    Value = false,
                    Name = $"Discrete Input {nextAddress}",
                    Description = "New discrete input",
                    IsRecentlyModified = false
                };
                
                // Temporarily disable discrete input changed notifications
                bool oldSuppressNotifications = newDiscreteInput.SuppressNotifications;
                newDiscreteInput.SuppressNotifications = true;
                
                try
                {
                    // Add to the service's collection
                    _slaveService.DiscreteInputDefinitions.Add(newDiscreteInput);
                    
                    // Hook up property change notification
                    if (newDiscreteInput is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += DiscreteInput_PropertyChanged;
                    }
                    
                    // Update the service's data store
                    _slaveService.UpdateDiscreteInputValue(newDiscreteInput);
                    
                    // Select the new discrete input
                    SelectedDiscreteInput = newDiscreteInput;
                    
                    // Notify UI that discrete inputs collection has changed
                    OnPropertyChanged(nameof(HasDiscreteInputs));
                }
                finally
                {
                    // Restore the original notification state
                    newDiscreteInput.SuppressNotifications = oldSuppressNotifications;
                }
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Added discrete input at address {nextAddress}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to add discrete input: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Remove the currently selected discrete input from the slave discrete input collection
        /// </summary>
        private void RemoveDiscreteInput()
        {
            if (SelectedDiscreteInput == null)
            {
                return;
            }
            
            try
            {
                var discreteInputToRemove = SelectedDiscreteInput;
                SelectedDiscreteInput = null; // Clear selection before removing to avoid UI issues
                
                // Remove from the service's collection
                _slaveService.DiscreteInputDefinitions.Remove(discreteInputToRemove);
                
                // Unsubscribe from property changed events
                if (discreteInputToRemove is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged -= DiscreteInput_PropertyChanged;
                }
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Removed discrete input at address {discreteInputToRemove.Address}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to remove discrete input: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Import coils from a file
        /// </summary>
        private void ImportCoils()
        {
            try
            {
                // For now, this is a placeholder implementation
                // In a real implementation, this would open a file dialog and parse the file contents
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Import coils feature is not yet implemented"));
                
                // Note: When implementing, follow the same pattern as ImportRegisters()
                // 1. Open file dialog
                // 2. Parse file (CSV, JSON, etc.)
                // 3. Create BooleanRegisterDefinition objects
                // 4. Add to _slaveService.CoilDefinitions
                // 5. Update UI
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to import coils: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Export coils to a file
        /// </summary>
        private void ExportCoils()
        {
            try
            {
                // For now, this is a placeholder implementation
                // In a real implementation, this would open a save dialog and write the coil data to a file
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Export coils feature is not yet implemented"));
                
                // Note: When implementing, follow the same pattern as ExportRegisters()
                // 1. Open save dialog
                // 2. Format coil data (CSV, JSON, etc.)
                // 3. Write to file
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to export coils: {ex.Message}"));
            }
        }

        /// <summary>
        /// Import discrete inputs from a file
        /// </summary>
        private void ImportDiscreteInputs()
        {
            try
            {
                // For now, this is a placeholder implementation
                // In a real implementation, this would open a file dialog and parse the file contents
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Import discrete inputs feature is not yet implemented"));
                
                // Note: When implementing, follow the same pattern as ImportRegisters()
                // 1. Open file dialog
                // 2. Parse file (CSV, JSON, etc.)
                // 3. Create BooleanRegisterDefinition objects
                // 4. Add to _slaveService.DiscreteInputDefinitions
                // 5. Update UI
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to import discrete inputs: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Export discrete inputs to a file
        /// </summary>
        private void ExportDiscreteInputs()
        {
            try
            {
                // For now, this is a placeholder implementation
                // In a real implementation, this would open a save dialog and write the discrete input data to a file
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Export discrete inputs feature is not yet implemented"));
                
                // Note: When implementing, follow the same pattern as ExportRegisters()
                // 1. Open save dialog
                // 2. Format discrete input data (CSV, JSON, etc.)
                // 3. Write to file
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to export discrete inputs: {ex.Message}"));
            }
        }
        
        private void HighlightTimer_Tick(object? sender, EventArgs e)
        {
            _highlightTimer.Stop();
            
            // Need to update UI on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Clear highlights for registers
                foreach (var register in _highlightedRegisters)
                {
                    register.IsRecentlyModified = false;
                }
                _highlightedRegisters.Clear();
                
                // Clear highlights for coils
                foreach (var coil in _highlightedCoils)
                {
                    coil.IsRecentlyModified = false;
                }
                _highlightedCoils.Clear();
                
                // Clear highlights for discrete inputs
                foreach (var discreteInput in _highlightedDiscreteInputs)
                {
                    discreteInput.IsRecentlyModified = false;
                }
                _highlightedDiscreteInputs.Clear();
            });
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
                // Set status to disconnected while attempting connection
                ConnectionStatus = ConnectionStatus.Disconnected;
                
                // Select the appropriate service based on mode
                _currentService = IsMasterMode ? _masterService : _slaveService;
                
                // Connect using the current parameters
                IsConnected = await _currentService.ConnectAsync(_connectionParameters);
                
                // Update connection status based on result
                ConnectionStatus = IsConnected ? ConnectionStatus.Connected : ConnectionStatus.Failed;
                
                // Update commands
                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                SendRequestCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Connection error: {ex.Message}"));
                IsConnected = false;
                ConnectionStatus = ConnectionStatus.Failed;
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

                // Update connection status
                ConnectionStatus = ConnectionStatus.Disconnected;

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

                // This will trigger the LastResponse setter which updates status and items
                // response could be null here, but LastResponse is already marked as nullable
                LastResponse = response;

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
        /// Determines if a device scan can be started
        /// </summary>
        private bool CanScanForDevices()
        {
            return _currentService != null && 
                   IsConnected && 
                   IsMasterMode && 
                   !IsDeviceScanActive && 
                   ConnectionParameters is RtuConnectionParameters;
        }
        
        /// <summary>
        /// Determines if an active device scan can be stopped
        /// </summary>
        private bool CanStopDeviceScan()
        {
            return IsDeviceScanActive;
        }
        
        /// <summary>
        /// Starts scanning for Modbus devices on the current RTU connection
        /// </summary>
        private void StartDeviceScan()
        {
            if (!(_currentService is ModbusMasterService masterService) ||
                !(ConnectionParameters is RtuConnectionParameters) ||
                !IsConnected ||
                IsDeviceScanActive)
            {
                return;
            }

            try
            {
                // Clear previous scan results
                _deviceScanResults.Clear();
                
                // Create cancellation token source
                _deviceScanCts = new CancellationTokenSource();
                
                // Set scanning state
                IsDeviceScanActive = true;
                
                // Update the Response panel
                ResponseStatus = "Scanning for Modbus devices...";
                // Clear response items to prepare for scan results
                ResponseItems.Clear();
                
                // Log the start of scanning
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Starting device scan..."));
                
                // Start the scan asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await masterService.ScanForDevicesAsync(_deviceScanCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                            OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Device scan cancelled")));
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                            OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Device scan error: {ex.Message}")));
                    }
                    finally
                    {
                        // Reset scan state
                        IsDeviceScanActive = false;
                        _deviceScanCts = null;
                        
                        // Update UI and command states
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ScanForDevicesCommand.RaiseCanExecuteChanged();
                            StopDeviceScanCommand.RaiseCanExecuteChanged();
                            
                            // Show summary of found devices
                            var respondingDevices = _deviceScanResults.Count(r => r.ResponseStatus == Models.ResponseStatus.Success);
                            var errorDevices = _deviceScanResults.Count(r => r.ResponseStatus == Models.ResponseStatus.Exception);
                            var timeoutDevices = _deviceScanResults.Count - respondingDevices - errorDevices;
                            var scanSummary = $"Scan complete: {respondingDevices} device(s) responded successfully, {errorDevices} with exceptions, {timeoutDevices} timed out";
                            
                            // Update both the event log and response panel
                            OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent(scanSummary));
                            ResponseStatus = scanSummary;
                            
                            // At this point we've already been adding scan results to the response items
                            // Just refresh the UI and sort the results by slave ID
                            if (ResponseItems.Count > 0)
                            {
                                // Make a copy of the current items
                                var sortedItems = ResponseItems.OrderBy(i => i.Address).ToList();
                                
                                // Clear and repopulate in sorted order
                                ResponseItems.Clear();
                                foreach (var item in sortedItems)
                                {
                                    ResponseItems.Add(item);
                                }
                                
                                // Refresh UI
                                OnPropertyChanged(nameof(HasLastResponse));
                            }
                            // Update UI to show the response items
                            OnPropertyChanged(nameof(HasLastResponse));
                        });
                    }
                });
                
                // Refresh command states
                ScanForDevicesCommand.RaiseCanExecuteChanged();
                StopDeviceScanCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to start device scan: {ex.Message}"));
                IsDeviceScanActive = false;
            }
        }
        
        /// <summary>
        /// Stops an active device scan
        /// </summary>
        private void StopDeviceScan()
        {
            if (!IsDeviceScanActive || _deviceScanCts == null)
            {
                return;
            }

            try
            {
                // Cancel the scan operation
                _deviceScanCts.Cancel();
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Stopping device scan..."));
                
                // Command states will be updated when the scan task completes in the finally block
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Error stopping device scan: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Handles device scan result events
        /// </summary>
        private void OnDeviceScanResultReceived(object? sender, DeviceScanResult result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Add to collection
                _deviceScanResults.Add(result);
                
                // Create a communication event for each result
                string status;
                switch (result.ResponseStatus)
                {
                    case Models.ResponseStatus.Success:
                        status = "responded successfully";
                        OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Device ID {result.SlaveId} {status} in {result.ResponseTime:F1} ms"));
                        
                        // Update response time display
                        ResponseTime = $"{result.ResponseTime:F1} ms";
                        
                        // Add to response items for display in the table
                        ResponseItems.Add(new ModbusResponseItem
                        {
                            Address = result.SlaveId,
                            Value = $"{result.ResponseTime:F1} ms"
                        });
                        break;
                        
                    case Models.ResponseStatus.Exception:
                        status = $"responded with exception: {result.ExceptionMessage}";
                        OnCommunicationEvent(this, CommunicationEvent.CreateWarningEvent($"Device ID {result.SlaveId} {status}"));
                        
                        // Add to response items with exception info
                        ResponseItems.Add(new ModbusResponseItem
                        {
                            Address = result.SlaveId,
                            Value = $"Exception: {result.ExceptionMessage}"
                        });
                        break;
                        
                    case Models.ResponseStatus.Timeout:
                    default:
                        status = "timed out";
                        // Don't log timeouts to avoid flooding the log
                        // We also don't add timeouts to the response items to avoid clutter
                        break;
                }
                
                // Update the Response panel with current scan progress
                var respondingDevices = _deviceScanResults.Count(r => r.ResponseStatus == Models.ResponseStatus.Success);
                var errorDevices = _deviceScanResults.Count(r => r.ResponseStatus == Models.ResponseStatus.Exception);
                var timeoutDevices = _deviceScanResults.Count(r => r.ResponseStatus == Models.ResponseStatus.Timeout);
                
                ResponseStatus = $"Scanning: ID {result.SlaveId} - {respondingDevices} devices responded, {errorDevices} exceptions, {timeoutDevices} timeouts";
                
                // Update the UI
                OnPropertyChanged(nameof(DeviceScanResults));
                OnPropertyChanged(nameof(HasLastResponse));
            });
        }

        // SaveConnection and LoadConnection methods are implemented elsewhere

        /// <summary>
        /// Gets or sets the current connection type
        /// </summary>
        public ConnectionType ConnectionType 
        {
            get => ConnectionParameters?.Type ?? ConnectionType.TCP;
            set 
            {
                if (ConnectionType != value)
                {
                    ChangeConnectionType(value);
                }
            }
        }

        /// <summary>
        /// Gets whether the current connection is TCP mode
        /// </summary>
        public bool IsTcpMode => ConnectionType == ConnectionType.TCP;
        
        /// <summary>
        /// Gets whether the current connection is RTU mode
        /// </summary>
        public bool IsRtuMode => ConnectionType == ConnectionType.RTU;
        
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

                // Notify all related properties
                OnPropertyChanged(nameof(ConnectionParameters));
                OnPropertyChanged(nameof(ConnectionType));
                OnPropertyChanged(nameof(IsTcpMode));
                OnPropertyChanged(nameof(IsRtuMode));
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
        /// Removes the last write data item from the collection
        /// </summary>
        private void RemoveLastWriteDataItem()
        {
            if (_writeDataInputs.Count > 1)
            {
                var lastItem = _writeDataInputs[_writeDataInputs.Count - 1];
                lastItem.OnDataTypeChanged -= WriteDataItem_DataTypeChanged;
                _writeDataInputs.Remove(lastItem);

                CalculateWriteQuantity();

                // Force command availability to update
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Determines if the last write data item can be removed
        /// </summary>
        private bool CanRemoveLastWriteDataItem()
        {
            return _writeDataInputs.Count > 1 && IsMultipleWriteFunction;
        }

        /// <summary>
        /// Event handler for when a write data item's data type changes
        /// </summary>
        private void WriteDataItem_DataTypeChanged(object? sender, EventArgs e)
        {
            CalculateWriteQuantity();
        }
        
        /// <summary>
        /// Add a new holding register to the slave register collection
        /// </summary>
        private void AddRegister()
        {
            try
            {
                // Find the next available address
                ushort nextAddress = 0;
                if (_slaveService.RegisterDefinitions.Count > 0)
                {
                    var last = _slaveService.RegisterDefinitions.OrderBy(r => r.Address).Last();
                    nextAddress = (ushort)(last.Address + last.RegisterCount);
                }
                
                // Create a new register with default values
                var newRegister = new RegisterDefinition
                {
                    Address = nextAddress,
                    Value = 0,
                    Name = $"Register {nextAddress}",
                    Description = "New holding register",
                    DataType = ModbusDataType.UInt16,
                    // Ensure the new register doesn't have IsRecentlyModified set
                    IsRecentlyModified = false
                };
                
                // Temporarily disable register changed notifications
                bool oldSuppressNotifications = newRegister.SuppressNotifications;
                newRegister.SuppressNotifications = true;
                
                try
                {
                    // Add to the service's collection (this must happen first)
                    _slaveService.RegisterDefinitions.Add(newRegister);
                    
                    // Hook up property change notification
                    if (newRegister is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += Register_PropertyChanged;
                    }
                    
                    // Update the service's data store
                    _slaveService.UpdateRegisterValue(newRegister);
                    
                    // Select the new register
                    SelectedRegister = newRegister;
                    
                    // Notify UI that registers collection has changed
                    OnPropertyChanged(nameof(HasRegisters));
                }
                finally
                {
                    // Restore the original notification state
                    newRegister.SuppressNotifications = oldSuppressNotifications;
                }
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Added holding register at address {nextAddress}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to add holding register: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Add a new input register to the slave register collection
        /// </summary>
        private void AddInputRegister()
        {
            try
            {
                // Find the next available address
                ushort nextAddress = 0;
                if (_slaveService.InputRegisterDefinitions.Count > 0)
                {
                    var last = _slaveService.InputRegisterDefinitions.OrderBy(r => r.Address).Last();
                    nextAddress = (ushort)(last.Address + last.RegisterCount);
                }
                
                // Create a new register with default values
                var newRegister = new RegisterDefinition
                {
                    Address = nextAddress,
                    Value = 0,
                    Name = $"Input {nextAddress}",
                    Description = "New input register",
                    DataType = ModbusDataType.UInt16
                };
                
                // Add to the service's collection
                _slaveService.InputRegisterDefinitions.Add(newRegister);
                
                // Hook up property change notification
                if (newRegister is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += InputRegister_PropertyChanged;
                }
                
                // Update the service's data store
                _slaveService.UpdateInputRegisterValue(newRegister);
                
                // Select the new register
                SelectedInputRegister = newRegister;
                
                // Notify UI that input registers collection has changed
                OnPropertyChanged(nameof(HasInputRegisters));
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Added input register at address {nextAddress}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to add input register: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Remove the selected input register from the slave input register collection
        /// </summary>
        private void RemoveInputRegister()
        {
            if (SelectedInputRegister == null) return;
            
            try
            {
                // Keep a reference to remove
                var registerToRemove = SelectedInputRegister;
                
                // Clear selection first to avoid any issues
                SelectedInputRegister = null;
                
                // Remove from service
                _slaveService.InputRegisterDefinitions.Remove(registerToRemove);
                
                // Unhook property change notification
                if (registerToRemove is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged -= InputRegister_PropertyChanged;
                }
                
                // Notify UI
                OnPropertyChanged(nameof(HasInputRegisters));
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Removed input register at address {registerToRemove.Address}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to remove input register: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Clear all input registers from the slave input register collection
        /// </summary>
        private void ClearInputRegisters()
        {
            try
            {
                // Clear selection first
                SelectedInputRegister = null;
                
                // Clear the collection
                var registers = _slaveService.InputRegisterDefinitions.ToList();
                foreach (var register in registers)
                {
                    // Unhook property change notification
                    if (register is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged -= InputRegister_PropertyChanged;
                    }
                    
                    _slaveService.InputRegisterDefinitions.Remove(register);
                }
                
                // Notify UI
                OnPropertyChanged(nameof(HasInputRegisters));
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("All input registers cleared"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to clear input registers: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Edit the currently selected input register
        /// </summary>
        private void EditInputRegister()
        {
            if (SelectedInputRegister == null) return;
            
            // In this simple implementation, we just ensure the register data is updated in the datastore
            try
            {
                _slaveService.UpdateInputRegisterValue(SelectedInputRegister);
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent($"Updated input register at address {SelectedInputRegister.Address}"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to update input register: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Import input registers from a file
        /// </summary>
        private void ImportInputRegisters()
        {
            try
            {
                // For now, this is just a placeholder
                // In a real implementation, this would open a file dialog and import registers from a CSV or JSON file
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Input register import not implemented yet"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to import input registers: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Export input registers to a file
        /// </summary>
        private void ExportInputRegisters()
        {
            try
            {
                // For now, this is just a placeholder
                // In a real implementation, this would open a file dialog and export registers to a CSV or JSON file
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Input register export not implemented yet"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to export input registers: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Remove the selected register from the slave register collection
        /// </summary>
        private void RemoveRegister()
        {
            if (SelectedRegister == null) return;
            
            try
            {
                // Keep a reference to remove
                var registerToRemove = SelectedRegister;
                
                // Clear selection first to avoid any issues
                SelectedRegister = null;
                
                // Remove from service
                _slaveService.RemoveRegister(registerToRemove);
                
                // Notify UI
                OnPropertyChanged(nameof(HasRegisters));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to remove register: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Clear all registers from the slave register collection
        /// </summary>
        private void ClearRegisters()
        {
            try
            {
                // Clear selection first
                SelectedRegister = null;
                
                // Clear the collection
                var registers = _slaveService.RegisterDefinitions.ToList();
                foreach (var register in registers)
                {
                    _slaveService.RemoveRegister(register);
                }
                
                // Notify UI
                OnPropertyChanged(nameof(HasRegisters));
                
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("All registers cleared"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to clear registers: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Edit the currently selected register
        /// </summary>
        private void EditRegister()
        {
            if (SelectedRegister == null) return;
            
            // In this simple implementation, we just ensure the register data is updated in the datastore
            try
            {
                _slaveService.UpdateRegisterValue(SelectedRegister);
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to update register: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Import registers from a file
        /// </summary>
        private void ImportRegisters()
        {
            try
            {
                // For now, this is just a placeholder
                // In a real implementation, this would open a file dialog and import registers from a CSV or JSON file
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Register import not implemented yet"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to import registers: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Export registers to a file
        /// </summary>
        private void ExportRegisters()
        {
            try
            {
                // For now, this is just a placeholder
                // In a real implementation, this would open a file dialog and export registers to a CSV or JSON file
                OnCommunicationEvent(this, CommunicationEvent.CreateInfoEvent("Register export not implemented yet"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to export registers: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Initialize the available data types collection
        /// </summary>
        private void InitializeDataTypes()
        {
            // Clear existing items
            _availableDataTypes.Clear();
            
            // Add all available data types
            _availableDataTypes.Add(ModbusDataType.UInt16);
            _availableDataTypes.Add(ModbusDataType.Int16);
            _availableDataTypes.Add(ModbusDataType.UInt32);
            _availableDataTypes.Add(ModbusDataType.Int32);
            _availableDataTypes.Add(ModbusDataType.Float32);
            _availableDataTypes.Add(ModbusDataType.Float64);
            _availableDataTypes.Add(ModbusDataType.Hex);
            _availableDataTypes.Add(ModbusDataType.Binary);
            
            // Notify UI of changes
            OnPropertyChanged(nameof(AvailableDataTypes));
        }
        
        /// <summary>
        /// Handle collection changes in the RegisterDefinitions collection
        /// </summary>
        private void RegisterDefinitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                // Handle added registers
                foreach (RegisterDefinition newRegister in e.NewItems)
                {
                    if (newRegister is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += Register_PropertyChanged;
                    }
                }
            }
            
            if (e.OldItems != null)
            {
                // Handle removed registers
                foreach (RegisterDefinition oldRegister in e.OldItems)
                {
                    if (oldRegister is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged -= Register_PropertyChanged;
                    }
                }
            }
            
            // Notify UI properties that depend on register count
            OnPropertyChanged(nameof(HasRegisters));
        }
        
        /// <summary>
        /// Handle collection changes in the InputRegisterDefinitions collection
        /// </summary>
        private void InputRegisterDefinitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                // Handle added input registers
                foreach (RegisterDefinition newRegister in e.NewItems)
                {
                    if (newRegister is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += InputRegister_PropertyChanged;
                    }
                }
            }
            
            if (e.OldItems != null)
            {
                // Handle removed input registers
                foreach (RegisterDefinition oldRegister in e.OldItems)
                {
                    if (oldRegister is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged -= InputRegister_PropertyChanged;
                    }
                }
            }
            
            // Notify UI properties that depend on register count
            OnPropertyChanged(nameof(HasInputRegisters));
        }
        
        /// <summary>
        /// Handle collection changes in the CoilDefinitions collection
        /// </summary>
        private void CoilDefinitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                // Handle added coils
                foreach (BooleanRegisterDefinition newCoil in e.NewItems)
                {
                    if (newCoil is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += Coil_PropertyChanged;
                    }
                }
            }
            
            if (e.OldItems != null)
            {
                // Handle removed coils
                foreach (BooleanRegisterDefinition oldCoil in e.OldItems)
                {
                    if (oldCoil is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged -= Coil_PropertyChanged;
                    }
                }
            }
            
            // Notify UI properties that depend on coil count
            OnPropertyChanged(nameof(HasCoils));
        }
        
        /// <summary>
        /// Handle collection changes in the DiscreteInputDefinitions collection
        /// </summary>
        private void DiscreteInputDefinitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                // Handle added discrete inputs
                foreach (BooleanRegisterDefinition newDiscreteInput in e.NewItems)
                {
                    if (newDiscreteInput is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged += DiscreteInput_PropertyChanged;
                    }
                }
            }
            
            if (e.OldItems != null)
            {
                // Handle removed discrete inputs
                foreach (BooleanRegisterDefinition oldDiscreteInput in e.OldItems)
                {
                    if (oldDiscreteInput is INotifyPropertyChanged notifyPropertyChanged)
                    {
                        notifyPropertyChanged.PropertyChanged -= DiscreteInput_PropertyChanged;
                    }
                }
            }
            
            // Notify UI properties that depend on discrete input count
            OnPropertyChanged(nameof(HasDiscreteInputs));
        }
        
        /// <summary>
        /// Handle property changes in RegisterDefinition objects
        /// </summary>
        private void Register_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When a register property changes, update the value in the Modbus data store
            if (sender is RegisterDefinition register && (e.PropertyName == nameof(RegisterDefinition.Value) || e.PropertyName == nameof(RegisterDefinition.DataType)))
            {
                try
                {
                    _slaveService.UpdateRegisterValue(register);
                }
                catch (Exception ex)
                {
                    // Log the error but don't crash
                    OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to update register {register.Address}: {ex.Message}"));
                }
            }
        }
        
        /// <summary>
        /// Handle property changes in InputRegisterDefinition objects
        /// </summary>
        private void InputRegister_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When an input register property changes, update the value in the Modbus data store
            if (sender is RegisterDefinition register && (e.PropertyName == nameof(RegisterDefinition.Value) || e.PropertyName == nameof(RegisterDefinition.DataType)))
            {
                try
                {
                    _slaveService.UpdateInputRegisterValue(register);
                }
                catch (Exception ex)
                {
                    // Log the error but don't crash
                    OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to update input register {register.Address}: {ex.Message}"));
                }
            }
        }
        
        /// <summary>
        /// Handle property changes in Coil objects
        /// </summary>
        private void Coil_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When a coil property changes, update the value in the Modbus data store
            if (sender is BooleanRegisterDefinition coil && e.PropertyName == nameof(BooleanRegisterDefinition.Value))
            {
                try
                {
                    // Update the coil value in the slave service
                    _slaveService.UpdateCoilValue(coil);
                }
                catch (Exception ex)
                {
                    // Log the error but don't crash
                    OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to update coil {coil.Address}: {ex.Message}"));
                }
            }
        }
        
        /// <summary>
        /// Handle property changes in DiscreteInput objects
        /// </summary>
        private void DiscreteInput_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When a discrete input property changes, update the value in the Modbus data store
            if (sender is BooleanRegisterDefinition discreteInput && e.PropertyName == nameof(BooleanRegisterDefinition.Value))
            {
                try
                {
                    // Update the discrete input value in the slave service
                    _slaveService.UpdateDiscreteInputValue(discreteInput);
                }
                catch (Exception ex)
                {
                    // Log the error but don't crash
                    OnCommunicationEvent(this, CommunicationEvent.CreateErrorEvent($"Failed to update discrete input {discreteInput.Address}: {ex.Message}"));
                }
            }
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

                case ModbusDataType.Float32:
                    if (float.TryParse(value, out float floatValue))
                    {
                        // For single register, we can only capture the first half of a float
                        // This is only useful in specific scenarios
                        byte[] bytes = BitConverter.GetBytes(floatValue);
                        parameters.Value = BitConverter.ToUInt16(bytes, 0);

                        // Show warning to user that float values need multiple registers
                        CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent(
                            "Float32 values require 2 registers. Only first half will be written. Use Write Multiple Registers for complete float value."));
                    }
                    else
                    {
                        throw new FormatException($"Invalid Float32 value: {value}");
                    }
                    break;

                default:
                    // For other types (Float64, etc.) that don't fit in a single register
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
        /// Loads the list of available profiles
        /// </summary>
        private async void LoadProfilesAsync()
        {
            try
            {
                // Load profiles from the service
                var profiles = await _profileService.GetProfileNamesAsync();
                
                // Update the collection
                _profiles.Clear();
                foreach (var profile in profiles)
                {
                    _profiles.Add(profile);
                }
                
                // Always ensure Default Profile exists
                if (!_profiles.Contains("Default Profile"))
                {
                    _profiles.Add("Default Profile");
                }
                
                // Notify UI
                OnPropertyChanged(nameof(Profiles));
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Failed to load profiles: {ex.Message}"));
            }
        }
        
        // Methods for coils are implemented earlier in this file.
        
        // Import and export methods for coils are implemented earlier in this file.
        
        // Methods for discrete inputs are implemented earlier in this file.
        
        /// <summary>
        /// Loads a profile by name
        /// </summary>
        private async void LoadProfileAsync(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return;
                
            try
            {
                // Check if we're currently connected
                if (IsConnected)
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateWarningEvent("Please disconnect before loading a profile"));
                    return;
                }
                
                // Load the profile
                ConnectionParameters? loadedParameters = await _profileService.LoadProfileAsync(profileName);
                
                if (loadedParameters != null)
                {
                    // Store the connection type before updating parameters
                    ConnectionType previousType = ConnectionParameters?.Type ?? ConnectionType.TCP;
                    ConnectionType newType = loadedParameters.Type;
                    
                    // Update the current parameters
                    ConnectionParameters = loadedParameters;
                    
                    // Update UI mode based on loaded parameters
                    IsMasterMode = loadedParameters.IsMaster;

                    // If connection type changed, raise property changed to update UI
                    if (previousType != newType)
                    {
                        // This will be handled by the ViewModel_PropertyChanged event in MainWindow
                        OnPropertyChanged(nameof(ConnectionParameters));
                        CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Changed connection type to {newType}"));
                    }
                    
                    CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Connection profile '{loadedParameters.ProfileName}' loaded successfully"));
                }
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Failed to load connection profile '{profileName}': {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Saves the current connection parameters to a JSON file
        /// </summary>
        private async void SaveConnection()
        {
            try
            {
                // Make sure we have valid connection parameters
                if (ConnectionParameters == null)
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent("No connection parameters to save"));
                    return;
                }
                
                // Show input dialog for profile name
                var inputDialog = new InputDialogWindow
                {
                    Title = "Save Profile",
                    Message = "Enter a name for this profile:",
                    Input = ConnectionParameters.ProfileName
                };
                
                if (inputDialog.ShowDialog() == true)
                {
                    string profileName = inputDialog.Input.Trim();
                    
                    // Check if name is valid
                    if (string.IsNullOrEmpty(profileName))
                    {
                        CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent("Profile name cannot be empty"));
                        return;
                    }
                    
                    // Check if profile already exists (except for Default Profile which can be overwritten)
                    if (_profiles.Contains(profileName) && profileName != "Default Profile")
                    {
                        // Show confirmation dialog
                        var result = System.Windows.MessageBox.Show(
                            $"A profile named '{profileName}' already exists. Do you want to overwrite it?",
                            "Confirm Overwrite",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);
                            
                        if (result != System.Windows.MessageBoxResult.Yes)
                            return;
                    }
                    
                    // Save the profile
                    bool success = await _profileService.SaveProfileAsync(ConnectionParameters, profileName);
                    
                    if (success)
                    {
                        // Update profile name in parameters
                        ConnectionParameters.ProfileName = profileName;
                        
                        // Add to profiles list if it's not there
                        if (!_profiles.Contains(profileName))
                        {
                            _profiles.Add(profileName);
                        }
                        
                        // Update selected profile
                        _selectedProfileName = profileName;
                        OnPropertyChanged(nameof(SelectedProfileName));
                        
                        CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Connection profile '{profileName}' saved successfully"));
                    }
                }
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Failed to save connection profile: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Removes the currently selected profile
        /// </summary>
        private async void RemoveProfile()
        {
            try
            {
                // Don't allow removing the Default Profile
                if (string.IsNullOrEmpty(SelectedProfileName) || SelectedProfileName == "Default Profile")
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateWarningEvent("Cannot remove the Default Profile"));
                    return;
                }
                
                // Don't allow removing a profile while connected
                if (IsConnected)
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateWarningEvent("Please disconnect before removing a profile"));
                    return;
                }
                
                // Ask for confirmation
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to remove the profile '{SelectedProfileName}'?",
                    "Confirm Profile Removal",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                
                if (result != System.Windows.MessageBoxResult.Yes)
                    return;
                
                // Delete the profile
                bool success = await _profileService.DeleteProfileAsync(SelectedProfileName);
                
                if (success)
                {
                    // Remove from profiles collection
                    _profiles.Remove(SelectedProfileName);
                    
                    // Load the default profile
                    SelectedProfileName = "Default Profile";
                    
                    CommunicationEvents.Add(CommunicationEvent.CreateInfoEvent($"Profile '{SelectedProfileName}' removed successfully"));
                }
                else
                {
                    CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Failed to remove profile '{SelectedProfileName}'"));
                }
            }
            catch (Exception ex)
            {
                CommunicationEvents.Add(CommunicationEvent.CreateErrorEvent($"Error removing profile: {ex.Message}"));
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
