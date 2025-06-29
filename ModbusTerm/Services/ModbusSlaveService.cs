using ModbusTerm.Models;
using NModbus;
using NModbus.Data;
using NModbus.Device;
using NModbus.Serial;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusTerm.Services
{
    /// <summary>
    /// Implementation of IModbusService for Modbus slave mode
    /// </summary>
    public class ModbusSlaveService : IModbusService, IDisposable
    {
        private IModbusSlave? _slave;
        private IModbusSlaveNetwork? _network;
        private TcpListener? _tcpListener;
        private SerialPort? _serialPort;
        private bool _isMaster = false;
        private readonly byte _slaveId = 1;
        private CancellationTokenSource? _cancellationTokenSource;
        private NotifyingSlaveDataStore? _dataStore;
        private ConnectionParameters? _currentParameters;
        private Dictionary<ushort, ushort> _registerValues = new Dictionary<ushort, ushort>();
        private bool _isConnected = false;
        private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

        /// <summary>
        /// Event raised when a communication event occurs
        /// </summary>
        public event EventHandler<CommunicationEvent>? CommunicationEventOccurred;
        
        /// <summary>
        /// Event raised when a holding register is changed by an external Modbus master
        /// </summary>
        public event EventHandler<RegisterChangedEventArgs>? RegisterChanged;
        
        /// <summary>
        /// Event raised when a coil is changed by an external Modbus master
        /// </summary>
        public event EventHandler<CoilChangedEventArgs>? CoilChanged;
        
        /// <summary>
        /// Event raised when a device scan result is received (not used in slave mode)
        /// Implemented only to satisfy the IModbusService interface
        /// </summary>
#pragma warning disable CS0067 // Event is never used
        public event EventHandler<DeviceScanResult>? DeviceScanResultReceived;
#pragma warning restore CS0067

        /// <summary>
        /// Gets whether a device scan is currently active (always false in slave mode)
        /// </summary>
        public bool IsDeviceScanActive => false;
        
        /// <summary>
        /// Gets whether the connection is currently open
        /// </summary>
        public bool IsConnected 
        { 
            get => _isConnected; 
            private set 
            { 
                _isConnected = value; 
                ConnectionStatus = value ? ConnectionStatus.Connected : ConnectionStatus.Disconnected; 
            } 
        }

        /// <summary>
        /// Gets the current connection status
        /// </summary>
        public ConnectionStatus ConnectionStatus 
        { 
            get => _connectionStatus; 
            private set => _connectionStatus = value; 
        }

        /// <summary>
        /// Gets whether the service is in master mode
        /// </summary>
        public bool IsMaster => _isMaster;

        /// <summary>
        /// Handle holding register changes from external Modbus masters
        /// </summary>
        /// <param name="sender">The object that raised the event</param>
        /// <param name="e">Event arguments with register addresses and values</param>
        private void DataStore_HoldingRegisterChanged(object? sender, RegisterChangedEventArgs e)
        {
            // Forward the event to any subscribers
            RegisterChanged?.Invoke(this, e);
            
            // Log the change event
            var addresses = string.Join(", ", Enumerable.Range(e.StartAddress, e.Values.Length)
                .Select(a => a.ToString()));
                
            RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent(
                $"Holding register(s) {addresses} modified by external Modbus master"));
        }
        
        /// <summary>
        /// Handle coil changes from external Modbus masters
        /// </summary>
        /// <param name="sender">The object that raised the event</param>
        /// <param name="e">Event arguments with coil addresses and values</param>
        private void DataStore_CoilChanged(object? sender, CoilChangedEventArgs e)
        {
            // Forward the event to any subscribers
            CoilChanged?.Invoke(this, e);
            
            // Log the change event
            var addresses = string.Join(", ", Enumerable.Range(e.StartAddress, e.Values.Length)
                .Select(a => a.ToString()));
                
            RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent(
                $"Coil(s) {addresses} modified by external Modbus master"));
        }

        /// <summary>
        /// Gets the holding register definitions for slave mode
        /// </summary>
        public ObservableCollection<RegisterDefinition> RegisterDefinitions { get; } = new ObservableCollection<RegisterDefinition>();

        /// <summary>
        /// Gets the input register definitions for slave mode
        /// </summary>
        public ObservableCollection<RegisterDefinition> InputRegisterDefinitions { get; } = new ObservableCollection<RegisterDefinition>();
        
        /// <summary>
        /// Gets the coil definitions for slave mode
        /// </summary>
        public ObservableCollection<BooleanRegisterDefinition> CoilDefinitions { get; } = new ObservableCollection<BooleanRegisterDefinition>();
        
        /// <summary>
        /// Gets the discrete input definitions for slave mode
        /// </summary>
        public ObservableCollection<BooleanRegisterDefinition> DiscreteInputDefinitions { get; } = new ObservableCollection<BooleanRegisterDefinition>();

        /// <summary>
        /// Constructor
        /// </summary>
        public ModbusSlaveService()
        {
            // Initialize with empty register collection and data store
            _dataStore = new NotifyingSlaveDataStore();
            _dataStore.HoldingRegisterChanged += DataStore_HoldingRegisterChanged;
            _dataStore.CoilChanged += DataStore_CoilChanged;
            InitializeRegisters();
        }

        /// <summary>
        /// Connect using the specified connection parameters
        /// </summary>
        public async Task<bool> ConnectAsync(ConnectionParameters parameters)
        {
            try
            {
                // Disconnect if already connected
                if (IsConnected)
                {
                    await DisconnectAsync();
                }

                _currentParameters = parameters;

                switch (parameters.Type)
                {
                    case ConnectionType.TCP:
                        return await StartTcpSlaveAsync(parameters as TcpConnectionParameters);
                    case ConnectionType.RTU:
                        return StartRtuSlave(parameters as RtuConnectionParameters);
                    default:
                        RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Unsupported connection type"));
                        return false;
                }
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Connection error: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Start TCP Modbus slave
        /// </summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> StartTcpSlaveAsync(TcpConnectionParameters? parameters)
        {
            if (parameters == null)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Invalid connection parameters"));
                return false;
            }

            try
            {
                // Create data store with notification support
                _dataStore = new NotifyingSlaveDataStore();
                
                // Subscribe to register and coil change events
                _dataStore.HoldingRegisterChanged += DataStore_HoldingRegisterChanged;
                _dataStore.CoilChanged += DataStore_CoilChanged;
                
                // Initialize registers with defined values
                InitializeRegisters();
                
                // Create TCP listener
                _tcpListener = new TcpListener(IPAddress.Any, parameters.Port);
                _tcpListener.Start();
                
                // Create Modbus TCP slave network
                var factory = new ModbusFactory();
                _network = factory.CreateSlaveNetwork(_tcpListener);
                
                // Create and attach the slave to the network
                _slave = factory.CreateSlave(_slaveId, _dataStore);
                _network.AddSlave(_slave);
                
                // Start listening for requests
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;
                
                // Listen for Modbus requests in background
                _ = Task.Run(async () => {
                    try {
                        while (!token.IsCancellationRequested) {
                            await _network.ListenAsync(token);
                        }
                    }
                    catch (OperationCanceledException) { /* Expected when cancellation requested */ }
                    catch (Exception ex) {
                        if (!token.IsCancellationRequested) {
                            RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"TCP slave error: {ex.Message}"));
                        }
                    }
                }, token);

                // Report success to UI
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Slave started on port {parameters.Port}"));
                
                return true;
            }
            catch (Exception ex)
            {
                StopTcpSlave();
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"TCP slave start error: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Stop TCP slave
        /// </summary>
        private void StopTcpSlave()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _network?.Dispose();
            _network = null;

            if (_tcpListener != null)
            {
                _tcpListener.Stop();
                _tcpListener = null;
            }
        }

        /// <summary>
        /// Start RTU Modbus slave
        /// </summary>
        private bool StartRtuSlave(RtuConnectionParameters? parameters)
        {
            if (parameters == null)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Invalid connection parameters"));
                return false;
            }

            try
            {
                // Create Modbus data store with notification support
                _dataStore = new NotifyingSlaveDataStore();
                
                // Subscribe to register and coil change events
                _dataStore.HoldingRegisterChanged += DataStore_HoldingRegisterChanged;
                _dataStore.CoilChanged += DataStore_CoilChanged;
                
                // Initialize registers with defined values
                InitializeRegisters();
                
                // Create and open serial port with correct parameters
                _serialPort = new SerialPort
                {
                    PortName = parameters.ComPort, // Use selected COM port
                    BaudRate = parameters.UseCustomBaudRate ? parameters.CustomBaudRate : parameters.BaudRate,
                    Parity = parameters.Parity,
                    DataBits = parameters.DataBits,
                    StopBits = parameters.StopBits
                };
                
                _serialPort.Open();
                
                // Create RTU slave network
                var factory = new ModbusFactory();
                var adapter = new SerialPortAdapter(_serialPort);
                _network = factory.CreateRtuSlaveNetwork(adapter);
                
                // Create and attach the slave to the network
                _slave = factory.CreateSlave(_slaveId, _dataStore);
                _network.AddSlave(_slave);
                
                // Start listening for requests
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;
                
                // Listen for Modbus requests in background
                Task.Run(() => {
                    try {
                        while (!token.IsCancellationRequested) {
                            _network.ListenAsync(token).Wait(1000);
                        }
                    }
                    catch (OperationCanceledException) { /* Expected when cancellation requested */ }
                    catch (Exception ex) {
                        if (!token.IsCancellationRequested) {
                            RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"RTU slave error: {ex.Message}"));
                        }
                    }
                }, token);

                // Report success to UI
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Slave started on {parameters.ComPort}"));

                return true;
            }
            catch (Exception ex)
            {
                StopRtuSlave();
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"RTU slave start error: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Stop RTU slave
        /// </summary>
        private void StopRtuSlave()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _network?.Dispose();
            _network = null;

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        /// <summary>
        /// Disconnect from the current connection
        /// </summary>
        public Task DisconnectAsync()
        {
            try
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource = null;
                }
                
                if (_network != null && _slave != null)
                {
                    // Stop the slave - NModbus doesn't have StopListening
                    _slave = null;
                    _network = null;
                    _dataStore = null;
                }
                
                _tcpListener?.Stop();
                _tcpListener = null;
                
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort?.Dispose();
                _serialPort = null;

                IsConnected = false;
                ConnectionStatus = ConnectionStatus.Disconnected;
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error disconnecting: {ex.Message}"));
                return Task.FromException(ex);
            }
        }
        
        /// <summary>
        /// Scan for devices - not implemented in slave mode
        /// </summary>
        /// <param name="cancellationToken">Cancellation token (not used in slave mode)</param>
        /// <returns>Completed task</returns>
        public Task ScanForDevicesAsync(CancellationToken cancellationToken)
        {
            // Device scanning is not supported in slave mode
            RaiseCommunicationEvent(CommunicationEvent.CreateWarningEvent("Device scanning is not available in slave mode"));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Method not used in slave mode
        /// </summary>
        public Task<object?> ExecuteRequestAsync(ModbusFunctionParameters parameters)
        {
            RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Cannot execute requests in slave mode"));
            return Task.FromResult<object?>(null);
        }

        /// <summary>
        /// Get a list of available COM ports
        /// </summary>
        public string[] GetAvailableComPorts()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Get a list of standard baud rates
        /// </summary>
        public int[] GetStandardBaudRates()
        {
            return new int[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
        }

        // Sample registers have been removed as per user request - users will now add their own registers
        
        /// <summary>
        /// Initialize the Modbus data store with register values from RegisterDefinitions and InputRegisterDefinitions
        /// </summary>
        private void InitializeRegisters()
        {
            if (_dataStore == null) return;
            
            // Initialize all defined holding registers in the data store
            foreach (var register in RegisterDefinitions)
            {
                // Prepare values based on data type
                var values = new List<ushort>();
                
                // Primary value is always included
                values.Add(register.Value);
                
                // Add additional values for multi-register types
                int registerCount = register.RegisterCount;
                if (registerCount > 1 && register.AdditionalValues.Count > 0)
                {
                    // Only add as many values as we need for this data type
                    int additionalCount = Math.Min(register.AdditionalValues.Count, registerCount - 1);
                    for (int i = 0; i < additionalCount; i++)
                    {
                        values.Add(register.AdditionalValues[i]);
                    }
                }
                
                // For data store registers, we need to use proper NModbus API methods
                // In NModbus 3.0.81, use WritePoints method on DefaultPointSource<ushort>
                _dataStore.HoldingRegisters.WritePoints(register.Address, values.ToArray());
            }
            
            // Initialize all defined input registers in the data store
            foreach (var register in InputRegisterDefinitions)
            {
                // Prepare values based on data type
                var values = new List<ushort>();
                
                // Primary value is always included
                values.Add(register.Value);
                
                // Add additional values for multi-register types
                int registerCount = register.RegisterCount;
                if (registerCount > 1 && register.AdditionalValues.Count > 0)
                {
                    // Only add as many values as we need for this data type
                    int additionalCount = Math.Min(register.AdditionalValues.Count, registerCount - 1);
                    for (int i = 0; i < additionalCount; i++)
                    {
                        values.Add(register.AdditionalValues[i]);
                    }
                }
                
                // For data store registers, we need to use proper NModbus API methods
                // In NModbus 3.0.81, use WritePoints method on DefaultPointSource<ushort>
                _dataStore.InputRegisters.WritePoints(register.Address, values.ToArray());
            }
            
            // Initialize all defined coils in the data store
            foreach (var coil in CoilDefinitions)
            {
                // Coils are just boolean values, so no need for additional processing
                _dataStore.CoilDiscretes.WritePoints(coil.Address, new bool[] { coil.Value });
            }
            
            // Initialize all defined discrete inputs in the data store
            foreach (var input in DiscreteInputDefinitions)
            {
                // Discrete inputs are just boolean values, so no need for additional processing
                _dataStore.CoilInputs.WritePoints(input.Address, new bool[] { input.Value });
            }
        }

        /// <summary>
        /// Update a holding register's value
        /// </summary>
        /// <param name="register">The register to update</param>
        public void UpdateRegisterValue(RegisterDefinition register)
        {
            try
            {
                if (_dataStore == null)
                {
                    RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Cannot update register {register.Address}: Data store not initialized"));
                    return;
                }
                
                // Prepare values based on data type
                var values = new List<ushort>();
                
                // Primary value is always included
                values.Add(register.Value);
                
                // Add additional values for multi-register types
                int registerCount = register.RegisterCount;
                if (registerCount > 1 && register.AdditionalValues.Count > 0)
                {
                    // Only add as many values as we need for this data type
                    int additionalCount = Math.Min(register.AdditionalValues.Count, registerCount - 1);
                    for (int i = 0; i < additionalCount; i++)
                    {
                        values.Add(register.AdditionalValues[i]);
                    }
                }
                
                // Temporarily suppress notifications for internal updates
                if (_dataStore.HoldingRegisters is NotifyingPointSource<ushort> notifyingSource)
                {
                    notifyingSource.SuppressNotifications = true;
                    try
                    {
                        // Update the register in the data store using WritePoints method
                        _dataStore.HoldingRegisters.WritePoints(register.Address, values.ToArray());
                    }
                    finally
                    {
                        // Make sure we re-enable notifications
                        notifyingSource.SuppressNotifications = false;
                    }
                }
                else
                {
                    // If not a notifying source (shouldn't happen), just call directly
                    _dataStore.HoldingRegisters.WritePoints(register.Address, values.ToArray());
                }
                
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Updated holding register {register.Address} to {register.FormattedValue}"));
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Failed to update holding register {register.Address}: {ex.Message}"));
            }
        }

        /// <summary>
        /// Update an input register's value
        /// </summary>
        /// <param name="register">The register to update</param>
        public void UpdateInputRegisterValue(RegisterDefinition register)
        {
            try
            {
                if (_dataStore == null)
                {
                    RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Cannot update input register {register.Address}: Data store not initialized"));
                    return;
                }
                
                // Prepare values based on data type
                var values = new List<ushort>();
                
                // Primary value is always included
                values.Add(register.Value);
                
                // Add additional values for multi-register types
                int registerCount = register.RegisterCount;
                if (registerCount > 1 && register.AdditionalValues.Count > 0)
                {
                    // Only add as many values as we need for this data type
                    int additionalCount = Math.Min(register.AdditionalValues.Count, registerCount - 1);
                    for (int i = 0; i < additionalCount; i++)
                    {
                        values.Add(register.AdditionalValues[i]);
                    }
                }
                
                // Temporarily suppress notifications for internal updates
                if (_dataStore.InputRegisters is NotifyingPointSource<ushort> notifyingSource)
                {
                    notifyingSource.SuppressNotifications = true;
                    try
                    {
                        // Update the register in the data store using WritePoints method
                        _dataStore.InputRegisters.WritePoints(register.Address, values.ToArray());
                    }
                    finally
                    {
                        // Make sure we re-enable notifications
                        notifyingSource.SuppressNotifications = false;
                    }
                }
                else
                {
                    // If not a notifying source (shouldn't happen), just call directly
                    _dataStore.InputRegisters.WritePoints(register.Address, values.ToArray());
                }
                
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Updated input register {register.Address} to {register.FormattedValue}"));
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Failed to update input register {register.Address}: {ex.Message}"));
            }
        }

        /// <summary>
        /// Add a new register definition
        /// </summary>
        public void AddRegister(RegisterDefinition register)
        {
            // Check if register address already exists
            var existing = RegisterDefinitions.FirstOrDefault(r => r.Address == register.Address);
            if (existing != null)
            {
                throw new InvalidOperationException($"Register at address {register.Address} already exists");
            }

            // Check if register would overlap multi-register types
            var potentialOverlaps = RegisterDefinitions.Where(r => 
                r.Address < register.Address && 
                r.Address + r.RegisterCount > register.Address);

            if (potentialOverlaps.Any())
            {
                var overlap = potentialOverlaps.First();
                throw new InvalidOperationException($"Register at address {register.Address} would overlap with {overlap.Name} ({overlap.Address}-{overlap.Address + overlap.RegisterCount - 1})");
            }

            // Check if register would be overlapped by multi-register types
            for (int i = 1; i < register.RegisterCount; i++)
            {
                var conflictingReg = RegisterDefinitions.FirstOrDefault(r => r.Address == register.Address + i);
                if (conflictingReg != null)
                {
                    throw new InvalidOperationException($"Register at address {register.Address} with size {register.RegisterCount} would overlap with existing register at {conflictingReg.Address}");
                }
            }

            RegisterDefinitions.Add(register);
            UpdateRegisterValue(register);
        }

        /// <summary>
        /// Remove a register definition
        /// </summary>
        public void RemoveRegister(RegisterDefinition register)
        {
            RegisterDefinitions.Remove(register);
        }

        /// <summary>
        /// Import register definitions from a file
        /// </summary>
        public void ImportRegisters(string filePath)
        {
            // Implementation would parse a file and add the registers
            throw new NotImplementedException();
        }

        /// <summary>
        /// Export register definitions to a file
        /// </summary>
        public void ExportRegisters(string filePath)
        {
            // Implementation would export the register definitions to a file
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Update a coil's value
        /// </summary>
        /// <param name="coil">The coil to update</param>
        public void UpdateCoilValue(BooleanRegisterDefinition coil)
        {
            try
            {
                if (_dataStore == null)
                {
                    RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Cannot update coil {coil.Address}: Data store not initialized"));
                    return;
                }
                
                // Temporarily suppress notifications for internal updates
                if (_dataStore.CoilDiscretes is NotifyingPointSource<bool> notifyingSource)
                {
                    notifyingSource.SuppressNotifications = true;
                    try
                    {
                        // Update the coil in the data store
                        _dataStore.CoilDiscretes.WritePoints(coil.Address, new bool[] { coil.Value });
                    }
                    finally
                    {
                        // Make sure we re-enable notifications
                        notifyingSource.SuppressNotifications = false;
                    }
                }
                else
                {
                    // If not a notifying source (shouldn't happen), just call directly
                    _dataStore.CoilDiscretes.WritePoints(coil.Address, new bool[] { coil.Value });
                }
                
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Updated coil {coil.Address} to {coil.FormattedValue}"));
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Failed to update coil {coil.Address}: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Update a discrete input's value
        /// </summary>
        /// <param name="input">The discrete input to update</param>
        public void UpdateDiscreteInputValue(BooleanRegisterDefinition input)
        {
            try
            {
                if (_dataStore == null)
                {
                    RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Cannot update discrete input {input.Address}: Data store not initialized"));
                    return;
                }
                
                // Update the discrete input in the data store
                _dataStore.CoilInputs.WritePoints(input.Address, new bool[] { input.Value });
                
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Updated discrete input {input.Address} to {input.FormattedValue}"));
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Failed to update discrete input {input.Address}: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Add a new coil definition
        /// </summary>
        public void AddCoil(BooleanRegisterDefinition coil)
        {
            // Check if coil address already exists
            var existing = CoilDefinitions.FirstOrDefault(r => r.Address == coil.Address);
            if (existing != null)
            {
                throw new InvalidOperationException($"Coil at address {coil.Address} already exists");
            }

            CoilDefinitions.Add(coil);
            UpdateCoilValue(coil);
        }
        
        /// <summary>
        /// Add a new discrete input definition
        /// </summary>
        public void AddDiscreteInput(BooleanRegisterDefinition input)
        {
            // Check if discrete input address already exists
            var existing = DiscreteInputDefinitions.FirstOrDefault(r => r.Address == input.Address);
            if (existing != null)
            {
                throw new InvalidOperationException($"Discrete input at address {input.Address} already exists");
            }

            DiscreteInputDefinitions.Add(input);
            UpdateDiscreteInputValue(input);
        }
        
        /// <summary>
        /// Remove a coil definition
        /// </summary>
        public void RemoveCoil(BooleanRegisterDefinition coil)
        {
            CoilDefinitions.Remove(coil);
        }
        
        /// <summary>
        /// Remove a discrete input definition
        /// </summary>
        public void RemoveDiscreteInput(BooleanRegisterDefinition input)
        {
            DiscreteInputDefinitions.Remove(input);
        }

        /// <summary>
        /// Raise the CommunicationEventOccurred event
        /// </summary>
        private void RaiseCommunicationEvent(CommunicationEvent e)
        {
            CommunicationEventOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            DisconnectAsync().Wait();
        }
    }
}
