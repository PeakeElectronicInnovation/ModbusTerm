using ModbusTerm.Models;

namespace ModbusTerm.Services
{
    /// <summary>
    /// Interface for Modbus communication services
    /// </summary>
    public interface IModbusService
    {
        /// <summary>
        /// Event raised when a communication event occurs
        /// </summary>
        event EventHandler<CommunicationEvent> CommunicationEventOccurred;

        /// <summary>
        /// Gets whether the connection is currently open
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets whether the service is in master mode
        /// </summary>
        bool IsMaster { get; }

        /// <summary>
        /// Connect using the specified connection parameters
        /// </summary>
        /// <param name="parameters">Connection parameters</param>
        /// <returns>True if connected successfully, false otherwise</returns>
        Task<bool> ConnectAsync(ConnectionParameters parameters);

        /// <summary>
        /// Disconnect from the current connection
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Execute a Modbus request in master mode
        /// </summary>
        /// <param name="parameters">The function parameters</param>
        /// <returns>The response data</returns>
        Task<object?> ExecuteRequestAsync(ModbusFunctionParameters parameters);

        /// <summary>
        /// Get a list of available COM ports (for RTU mode)
        /// </summary>
        /// <returns>List of COM port names</returns>
        string[] GetAvailableComPorts();

        /// <summary>
        /// Get a list of standard baud rates
        /// </summary>
        /// <returns>List of standard baud rates</returns>
        int[] GetStandardBaudRates();
    }
}
