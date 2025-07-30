namespace ModbusTerm.Models
{
    /// <summary>
    /// Represents the connection status for the Modbus connection
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// No connection has been initiated
        /// </summary>
        Disconnected,
        
        /// <summary>
        /// Connection is active
        /// </summary>
        Connected,
        
        /// <summary>
        /// Connection attempt failed
        /// </summary>
        Failed,
        
        /// <summary>
        /// Slave mode: Master client is connected
        /// </summary>
        MasterConnected
    }
}
