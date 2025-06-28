namespace ModbusTerm.Models
{
    /// <summary>
    /// Base class for Modbus connection parameters
    /// </summary>
    public abstract class ConnectionParameters
    {
        /// <summary>
        /// Gets or sets the connection type (TCP or RTU)
        /// </summary>
        public ConnectionType Type { get; set; }

        /// <summary>
        /// Gets or sets whether the connection is in Master or Slave mode
        /// </summary>
        public bool IsMaster { get; set; } = true;

        /// <summary>
        /// Gets or sets the connection name/profile name
        /// </summary>
        public string ProfileName { get; set; } = "Default Profile";
        
        /// <summary>
        /// Gets or sets the request timeout in milliseconds. A value of 0 means no timeout.
        /// </summary>
        public int Timeout { get; set; } = 5000; // Default 5 seconds
    }

    /// <summary>
    /// Parameters for TCP Modbus connection
    /// </summary>
    public class TcpConnectionParameters : ConnectionParameters
    {
        /// <summary>
        /// Gets or sets the IP address of the Modbus device
        /// </summary>
        public string IpAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Gets or sets the port for Modbus TCP communication (default: 502)
        /// </summary>
        public int Port { get; set; } = 502;

        public TcpConnectionParameters()
        {
            Type = ConnectionType.TCP;
        }
    }

    /// <summary>
    /// Parameters for RTU Modbus connection
    /// </summary>
    public class RtuConnectionParameters : ConnectionParameters
    {
        /// <summary>
        /// Gets or sets the COM port name (e.g., "COM1")
        /// </summary>
        public string ComPort { get; set; } = "COM1";

        /// <summary>
        /// Gets or sets the baud rate for serial communication
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// Gets or sets the parity for serial communication
        /// </summary>
        public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;

        /// <summary>
        /// Gets or sets the number of data bits for serial communication
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// Gets or sets the number of stop bits for serial communication
        /// </summary>
        public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;

        /// <summary>
        /// Gets or sets whether a custom baud rate should be used
        /// </summary>
        public bool UseCustomBaudRate { get; set; } = false;

        /// <summary>
        /// Gets or sets the custom baud rate when UseCustomBaudRate is true
        /// </summary>
        public int CustomBaudRate { get; set; } = 19200;
        
        public RtuConnectionParameters()
        {
            Type = ConnectionType.RTU;
        }
    }
}
