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
        /// Execute a Modbus request
        /// </summary>
        public async Task<object?> ExecuteRequestAsync(ModbusFunctionParameters parameters)
        {
            if (_master == null)
            {
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent("Not connected"));
                return new ModbusResponseInfo
                {
                    IsSuccess = false,
                    ErrorMessage = "Not connected",
                    ExecutionTimeMs = 0
                };
            }

            var responseInfo = new ModbusResponseInfo();
            var startTime = DateTime.Now;
            
            try
            {
                // Track the request bytes in a communication event
                object? result = null;

                switch (parameters.FunctionCode)
                {
                    case ModbusFunctionCode.ReadCoils:
                        if (parameters is ReadFunctionParameters readCoils)
                        {
                            var coils = await _master.ReadCoilsAsync(parameters.SlaveId, parameters.StartAddress, readCoils.Quantity);
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(new byte[] { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readCoils.Quantity >> 8), (byte)(readCoils.Quantity & 0xFF) }));
                            result = coils;
                        }
                        break;

                    case ModbusFunctionCode.ReadDiscreteInputs:
                        if (parameters is ReadFunctionParameters readInputs)
                        {
                            var inputs = await _master.ReadInputsAsync(parameters.SlaveId, parameters.StartAddress, readInputs.Quantity);
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(new byte[] { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readInputs.Quantity >> 8), (byte)(readInputs.Quantity & 0xFF) }));
                            result = inputs;
                        }
                        break;

                    case ModbusFunctionCode.ReadHoldingRegisters:
                        if (parameters is ReadFunctionParameters readHolding)
                        {
                            var registers = await _master.ReadHoldingRegistersAsync(parameters.SlaveId, parameters.StartAddress, readHolding.Quantity);
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(new byte[] { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readHolding.Quantity >> 8), (byte)(readHolding.Quantity & 0xFF) }));
                            result = registers;
                        }
                        break;

                    case ModbusFunctionCode.ReadInputRegisters:
                        if (parameters is ReadFunctionParameters readInput)
                        {
                            var registers = await _master.ReadInputRegistersAsync(parameters.SlaveId, parameters.StartAddress, readInput.Quantity);
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(new byte[] { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(readInput.Quantity >> 8), (byte)(readInput.Quantity & 0xFF) }));
                            result = registers;
                        }
                        break;

                    case ModbusFunctionCode.WriteSingleCoil:
                        if (parameters is WriteSingleCoilParameters writeCoil)
                        {
                            await _master.WriteSingleCoilAsync(parameters.SlaveId, parameters.StartAddress, writeCoil.Value);
                            ushort value = writeCoil.Value ? (ushort)0xFF00 : (ushort)0x0000;
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(new byte[] { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(value >> 8), (byte)(value & 0xFF) }));
                            result = writeCoil.Value;
                        }
                        break;

                    case ModbusFunctionCode.WriteSingleRegister:
                        if (parameters is WriteSingleRegisterParameters writeReg)
                        {
                            await _master.WriteSingleRegisterAsync(parameters.SlaveId, parameters.StartAddress, writeReg.Value);
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(new byte[] { parameters.SlaveId, (byte)parameters.FunctionCode, 
                                (byte)(parameters.StartAddress >> 8), (byte)(parameters.StartAddress & 0xFF),
                                (byte)(writeReg.Value >> 8), (byte)(writeReg.Value & 0xFF) }));
                            result = writeReg.Value;
                        }
                        break;

                    case ModbusFunctionCode.WriteMultipleCoils:
                        if (parameters is WriteMultipleCoilsParameters writeCoils)
                        {
                            await _master.WriteMultipleCoilsAsync(parameters.SlaveId, parameters.StartAddress, writeCoils.Values.ToArray());
                            
                            // Build basic Modbus request for logging
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
                            
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(coilBytes.ToArray()));
                            result = writeCoils.Values;
                        }
                        break;

                    case ModbusFunctionCode.WriteMultipleRegisters:
                        if (parameters is WriteMultipleRegistersParameters writeRegs)
                        {
                            await _master.WriteMultipleRegistersAsync(parameters.SlaveId, parameters.StartAddress, writeRegs.Values.ToArray());
                            
                            // Build basic Modbus request for logging
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
                            
                            RaiseCommunicationEvent(CommunicationEvent.CreateSentEvent(regBytes.ToArray()));
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
            catch (Exception ex)
            {
                // Calculate execution time even for errors
                var endTime = DateTime.Now;
                responseInfo.ExecutionTimeMs = (int)(endTime - startTime).TotalMilliseconds;
                responseInfo.IsSuccess = false;
                responseInfo.ErrorMessage = ex.Message;
                
                RaiseCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Execution error: {ex.Message}"));
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
        /// Get a list of standard baud rates
        /// </summary>
        public int[] GetStandardBaudRates()
        {
            return new int[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
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
