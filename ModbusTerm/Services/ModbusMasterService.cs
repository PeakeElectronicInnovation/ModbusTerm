using ModbusTerm.Models;
using NModbus;
using NModbus.Serial;
using System.IO.Ports;
using System.Net.Sockets;

namespace ModbusTerm.Services
{
    /// <summary>
    /// Implementation of IModbusService for Modbus master mode
    /// </summary>
    public class ModbusMasterService : IModbusService
    {
        private IModbusMaster? _master;
        private TcpClient? _tcpClient;
        private SerialPort? _serialPort;
        private bool _isMaster = true;
        private ConnectionParameters? _currentParameters;
        private bool _needsConnectionRecovery = false;

        /// <summary>
        /// Event raised when a communication event occurs
        /// </summary>
        public event EventHandler<CommunicationEvent>? CommunicationEventOccurred;

        /// <summary>
        /// Gets whether the connection is currently open
        /// </summary>
        public bool IsConnected => _master != null && 
            (_tcpClient?.Connected == true || (_serialPort?.IsOpen == true));

        /// <summary>
        /// Gets whether the service is in master mode
        /// </summary>
        public bool IsMaster => _isMaster;

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
                        return await ConnectTcpAsync(parameters as TcpConnectionParameters);
                    case ConnectionType.RTU:
                        return ConnectRtu(parameters as RtuConnectionParameters);
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
        /// Connect to TCP Modbus device
        /// </summary>
        private async Task<bool> ConnectTcpAsync(TcpConnectionParameters? parameters)
        {
            if (parameters == null)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Invalid connection parameters"));
                return false;
            }

            try
            {
                // Create and connect TCP client
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(parameters.IpAddress, parameters.Port);

                // Create Modbus master
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);

                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Connected to TCP device at {parameters.IpAddress}:{parameters.Port}"));
                return true;
            }
            catch (Exception ex)
            {
                _tcpClient?.Close();
                _tcpClient = null;
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"TCP connection error: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Connect to RTU Modbus device
        /// </summary>
        private bool ConnectRtu(RtuConnectionParameters? parameters)
        {
            if (parameters == null)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Invalid connection parameters"));
                return false;
            }

            try
            {
                // Create and open serial port
                _serialPort = new SerialPort
                {
                    PortName = parameters.ComPort,
                    BaudRate = parameters.UseCustomBaudRate ? parameters.CustomBaudRate : parameters.BaudRate,
                    Parity = parameters.Parity,
                    DataBits = parameters.DataBits,
                    StopBits = parameters.StopBits
                };
                
                _serialPort.Open();

                // Create Modbus RTU master
                var factory = new ModbusFactory();
                var adapter = new SerialPortAdapter(_serialPort);
                _master = factory.CreateRtuMaster(adapter);

                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Connected to RTU device on {parameters.ComPort}"));
                return true;
            }
            catch (Exception ex)
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"RTU connection error: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Disconnect from the current connection
        /// </summary>
        public Task DisconnectAsync()
        {
            try
            {
                _master?.Dispose();
                _master = null;

                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }

                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }

                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent("Disconnected"));
            }
            catch (Exception ex)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Disconnect error: {ex.Message}"));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Execute a Modbus request with timeout from connection parameters
        /// </summary>
        public async Task<object?> ExecuteRequestAsync(ModbusFunctionParameters parameters)
        {
            if (_master == null)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Not connected"));
                return null;
            }
            
            // Check if the connection needs recovery from a previous timeout
            if (_needsConnectionRecovery && _currentParameters != null)
            {
                // Log the recovery attempt
                RaiseCommunicationEvent(CommunicationEvent.CreateInfoEvent("Recovering connection after timeout"));
                
                // Reset the connection
                await DisconnectAsync();
                await ConnectAsync(_currentParameters);
                
                // Clear the recovery flag regardless of reconnection success
                _needsConnectionRecovery = false;
                
                // If reconnection failed, return error
                if (_master == null)
                {
                    RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Failed to recover connection after timeout"));
                    return null;
                }
            }
            
            // Get the timeout value from the current connection parameters
            int timeout = _currentParameters?.Timeout ?? 5000; // Default to 5 seconds if not set

            var responseInfo = new ModbusResponseInfo();
            var startTime = DateTime.Now;
            
            try
            {
                // Create a cancellation token source for the timeout
                using CancellationTokenSource cts = timeout > 0 ? new CancellationTokenSource(timeout) : new CancellationTokenSource();
                
                // Track the request bytes in a communication event
                object? result = null;

                switch (parameters.FunctionCode)
                {
                    case ModbusFunctionCode.ReadCoils:
                        if (parameters is ReadFunctionParameters readCoils)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Read {readCoils.Quantity} coils from address {parameters.StartAddress} (FC1) - Slave ID {parameters.SlaveId}";
                            
                            // Create and log sent event
                            byte[] requestBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readCoils.Quantity >> 8), (byte)(readCoils.Quantity & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(requestBytes, sentMessage));
                            
                            // Execute the actual request with timeout token
                            var coils = await Task.Run(() => _master.ReadCoilsAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                readCoils.Quantity)
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response with raw data
                            // For boolean arrays, convert to byte array representation
                            byte[] responseBytes = GetByteArrayFromBooleans(coils);
                            string receivedMessage = $"Received {coils.Length} coil values from address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(responseBytes, receivedMessage));
                            
                            result = coils;
                        }
                        break;

                    case ModbusFunctionCode.ReadDiscreteInputs:
                        if (parameters is ReadFunctionParameters readDiscrete)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Read {readDiscrete.Quantity} discrete inputs from address {parameters.StartAddress} (FC2) - Slave ID {parameters.SlaveId}";
                            
                            // Create and log sent event
                            byte[] requestBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readDiscrete.Quantity >> 8), (byte)(readDiscrete.Quantity & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(requestBytes, sentMessage));
                            
                            // Execute the actual request with timeout token
                            var inputs = await Task.Run(() => _master.ReadInputsAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                readDiscrete.Quantity)
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response with raw data
                            // For boolean arrays, convert to byte array representation
                            byte[] responseBytes = GetByteArrayFromBooleans(inputs);
                            string receivedMessage = $"Received {inputs.Length} discrete input values from address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(responseBytes, receivedMessage));
                            
                            result = inputs;
                        }
                        break;
                        
                    case ModbusFunctionCode.ReadHoldingRegisters:
                        if (parameters is ReadFunctionParameters readHolding)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Read {readHolding.Quantity} holding registers from address {parameters.StartAddress} (FC3) - Slave ID {parameters.SlaveId}";
                            
                            // Create and log sent event
                            byte[] requestBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readHolding.Quantity >> 8), (byte)(readHolding.Quantity & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(requestBytes, sentMessage));
                            
                            // Execute the actual request with timeout token
                            var registers = await Task.Run(() => _master.ReadHoldingRegistersAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                readHolding.Quantity)
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response with raw data
                            byte[] responseBytes = GetByteArrayFromUshorts(registers);
                            string receivedMessage = $"Received {registers.Length} register values from address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(responseBytes, receivedMessage));
                            
                            result = registers;
                        }
                        break;

                    case ModbusFunctionCode.ReadInputRegisters:
                        if (parameters is ReadFunctionParameters readInput)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Read {readInput.Quantity} input registers from address {parameters.StartAddress} (FC4) - Slave ID {parameters.SlaveId}";
                            
                            // Create and log sent event
                            byte[] requestBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readInput.Quantity >> 8), (byte)(readInput.Quantity & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(requestBytes, sentMessage));
                            
                            // Execute the actual request with timeout token
                            var registers = await Task.Run(() => _master.ReadInputRegistersAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                readInput.Quantity)
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response with raw data
                            byte[] responseBytes = GetByteArrayFromUshorts(registers);
                            string receivedMessage = $"Received {registers.Length} input register values from address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(responseBytes, receivedMessage));
                            
                            result = registers;
                        }
                        break;

                    case ModbusFunctionCode.WriteSingleCoil:
                        if (parameters is WriteSingleCoilParameters writeCoil)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Write single coil value {(writeCoil.Value ? "ON" : "OFF")} to address {parameters.StartAddress} (FC5) - Slave ID {parameters.SlaveId}";
                            ushort value = writeCoil.Value ? (ushort)0xFF00 : (ushort)0x0000;
                            
                            // Create and log sent event
                            byte[] requestBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(value >> 8), (byte)(value & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(requestBytes, sentMessage));
                            
                            // Execute the actual request with timeout token
                            await Task.Run(() => _master.WriteSingleCoilAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                writeCoil.Value)
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response (echo response for write operations)
                            string receivedMessage = $"Write confirmed for coil at address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(requestBytes, receivedMessage));
                            
                            result = writeCoil.Value;
                        }
                        break;

                    case ModbusFunctionCode.WriteSingleRegister:
                        if (parameters is WriteSingleRegisterParameters writeReg)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Write single register value {writeReg.Value} to address {parameters.StartAddress} (FC6) - Slave ID {parameters.SlaveId}";
                            
                            // Create and log sent event
                            byte[] requestBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(writeReg.Value >> 8), (byte)(writeReg.Value & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(requestBytes, sentMessage));
                            
                            // Execute the actual request with timeout token
                            await Task.Run(() => _master.WriteSingleRegisterAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                writeReg.Value)
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response (echo response for write operations)
                            string receivedMessage = $"Write confirmed for register at address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(requestBytes, receivedMessage));
                            
                            result = writeReg.Value;
                        }
                        break;

                    case ModbusFunctionCode.WriteMultipleCoils:
                        if (parameters is WriteMultipleCoilsParameters writeCoils)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Write {writeCoils.Values.Count} coils to address {parameters.StartAddress} (FC15) - Slave ID {parameters.SlaveId}";
                            
                            // Build complete Modbus request for logging
                            var coilBytes = new List<byte>
                            { 
                                parameters.SlaveId, 
                                (byte)parameters.FunctionCode,
                                (byte)(parameters.StartAddress >> 8), 
                                (byte)(parameters.StartAddress & 0xFF),
                                (byte)(writeCoils.Values.Count >> 8), 
                                (byte)(writeCoils.Values.Count & 0xFF),
                                (byte)Math.Ceiling(writeCoils.Values.Count / 8.0)
                            };
                            
                            // Add the actual coil values to the log data
                            foreach (bool value in writeCoils.Values)
                            {
                                coilBytes.Add((byte)(value ? 0xFF : 0x00));
                            }
                            
                            // Create and log sent event
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(coilBytes.ToArray(), sentMessage));
                            
                            // Execute the actual request with timeout token
                            await Task.Run(() => _master.WriteMultipleCoilsAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                writeCoils.Values.ToArray())
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response (echo response for write operations)
                            string receivedMessage = $"Write confirmed for coils at address {parameters.StartAddress}";
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(coilBytes.ToArray(), receivedMessage));
                            
                            result = writeCoils.Values.ToArray();
                        }
                        break;

                    case ModbusFunctionCode.WriteMultipleRegisters:
                        if (parameters is WriteMultipleRegistersParameters writeRegs)
                        {
                            // Create descriptive message for sent event
                            string sentMessage = $"Write {writeRegs.Values.Count} registers to address {parameters.StartAddress} (FC16) - Slave ID {parameters.SlaveId}";
                            
                            // Build complete Modbus request for logging
                            var regBytes = new List<byte>
                            { 
                                parameters.SlaveId, 
                                (byte)parameters.FunctionCode,
                                (byte)(parameters.StartAddress >> 8), 
                                (byte)(parameters.StartAddress & 0xFF),
                                (byte)(writeRegs.Values.Count >> 8), 
                                (byte)(writeRegs.Values.Count & 0xFF),
                                (byte)(writeRegs.Values.Count * 2)
                            };
                            
                            // Add the actual register values to the log data
                            foreach (ushort value in writeRegs.Values)
                            {
                                regBytes.Add((byte)(value >> 8));    // High byte
                                regBytes.Add((byte)(value & 0xFF));  // Low byte
                            }
                            
                            // Create and log sent event
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(regBytes.ToArray(), sentMessage));
                            
                            // Execute the actual request with timeout token
                            await Task.Run(() => _master.WriteMultipleRegistersAsync(
                                parameters.SlaveId, 
                                parameters.StartAddress, 
                                writeRegs.Values.ToArray())
                            ).WaitAsync(cts.Token);
                            
                            // Log the received response (echo response for write operations)
                            string receivedMessage = $"Write confirmed for registers at address {parameters.StartAddress}";
                            byte[] responseBytes = { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(writeRegs.Values.Count >> 8), (byte)(writeRegs.Values.Count & 0xFF) };
                            RaiseCommunicationEvent(CommunicationEvent.CreateReceivedEvent(responseBytes, receivedMessage));
                            
                            result = writeRegs.Values;
                        }
                        break;

                    default:
                        RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Unsupported function code"));
                        return null;
                }

                // Calculate execution time
                var endTime = DateTime.Now;
                responseInfo.ExecutionTimeMs = (int)(endTime - startTime).TotalMilliseconds;
                responseInfo.IsSuccess = true;
                responseInfo.Data = result;
                
                return responseInfo;
            }
            catch (OperationCanceledException)
            {
                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                var timeoutMessage = $"Request timed out after {_currentParameters?.Timeout ?? 5000} ms";
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent(timeoutMessage));
                
                // Mark connection as needing recovery after timeout
                // This ensures subsequent requests don't also timeout
                _needsConnectionRecovery = true;
                
                return new ModbusResponseInfo
                {
                    IsSuccess = false,
                    ErrorMessage = timeoutMessage,
                    ExecutionTimeMs = (int)executionTime
                };
            }
            catch (Exception ex)
            {
                // Calculate execution time even for errors
                var endTime = DateTime.Now;
                responseInfo.ExecutionTimeMs = (int)(endTime - startTime).TotalMilliseconds;
                responseInfo.IsSuccess = false;
                responseInfo.ErrorMessage = ex.Message;
                
                // Create a more concise error message for SlaveExceptions
                string errorMessage;
                if (ex is NModbus.SlaveException slaveEx)
                {
                    errorMessage = $"Modbus Error: FC{(byte)slaveEx.FunctionCode} - Code {slaveEx.SlaveExceptionCode} - {GetSlaveExceptionMessage(slaveEx.SlaveExceptionCode)}";
                }
                else
                {
                    errorMessage = $"Execution error: {ex.Message.Split('.').FirstOrDefault() ?? ex.Message}";
                }
                
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent(errorMessage));
                return responseInfo;
            }
        }

        /// <summary>
        /// Get a list of available COM ports
        /// </summary>
        public string[] GetAvailableComPorts()
        {
            return SerialPort.GetPortNames();
        }
        
        /// <summary>
        /// Get a concise description for a Modbus slave exception code
        /// </summary>
        /// <param name="slaveExceptionCode">The exception code returned by the slave device</param>
        /// <returns>A short description of the exception</returns>
        private string GetSlaveExceptionMessage(byte slaveExceptionCode)
        {
            return slaveExceptionCode switch
            {
                1 => "Illegal Function",
                2 => "Illegal Data Address",
                3 => "Illegal Data Value",
                4 => "Slave Device Failure",
                5 => "Acknowledge",
                6 => "Slave Device Busy",
                8 => "Memory Parity Error",
                10 => "Gateway Path Unavailable",
                11 => "Gateway Target Device Failed to Respond",
                _ => $"Unknown Exception Code {slaveExceptionCode}"
            };
        }

        /// <summary>
        /// Get a list of standard baud rates
        /// </summary>
        public int[] GetStandardBaudRates()
        {
            return new int[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
        }

        /// <summary>
        /// Raise the CommunicationEvent
        /// </summary>
        private void RaiseCommunicationEvent(CommunicationEvent evt)
        {
            CommunicationEventOccurred?.Invoke(this, evt);
        }
        
        /// <summary>
        /// Converts a boolean array to a byte array suitable for logging
        /// </summary>
        /// <param name="booleans">Array of booleans to convert</param>
        /// <returns>Byte array representation</returns>
        private byte[] GetByteArrayFromBooleans(bool[] booleans)
        {
            if (booleans == null || booleans.Length == 0)
                return new byte[0];
                
            // Calculate how many bytes we need (each byte holds 8 booleans)
            int byteCount = (booleans.Length + 7) / 8;
            byte[] result = new byte[byteCount + 1]; // +1 for the count byte
            
            // First byte is the count of values
            result[0] = (byte)booleans.Length;
            
            // Pack the booleans into bytes
            for (int i = 0; i < booleans.Length; i++)
            {
                if (booleans[i])
                    result[1 + (i / 8)] |= (byte)(1 << (i % 8));
            }
            
            return result;
        }
        
        /// <summary>
        /// Converts a ushort array to a byte array suitable for logging
        /// </summary>
        /// <param name="ushorts">Array of ushorts to convert</param>
        /// <returns>Byte array representation</returns>
        private byte[] GetByteArrayFromUshorts(ushort[] ushorts)
        {
            if (ushorts == null || ushorts.Length == 0)
                return new byte[0];
                
            byte[] result = new byte[ushorts.Length * 2 + 1]; // 2 bytes per ushort + 1 byte for count
            
            // First byte is the count of values
            result[0] = (byte)ushorts.Length;
            
            // Convert each ushort to two bytes
            for (int i = 0; i < ushorts.Length; i++)
            {
                result[1 + (i * 2)] = (byte)(ushorts[i] >> 8);    // High byte
                result[1 + (i * 2) + 1] = (byte)(ushorts[i] & 0xFF); // Low byte
            }
            
            return result;
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
