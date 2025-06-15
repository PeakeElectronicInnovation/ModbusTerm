using System;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Contains metadata about a Modbus response
    /// </summary>
    public class ModbusResponseInfo
    {
        /// <summary>
        /// Gets or sets whether the request was successful
        /// </summary>
        public bool IsSuccess { get; set; } = true;
        
        /// <summary>
        /// Gets or sets any error message from the request
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the time taken to execute the request in milliseconds
        /// </summary>
        public int ExecutionTimeMs { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the response was received
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets the raw response data
        /// </summary>
        public object? Data { get; set; } = null;
    }
}
