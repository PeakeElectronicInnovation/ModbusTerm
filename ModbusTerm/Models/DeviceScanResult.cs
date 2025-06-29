using System;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Represents the status of a device scan response
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>
        /// The device responded successfully
        /// </summary>
        Success,
        
        /// <summary>
        /// The device responded with an exception
        /// </summary>
        Exception,
        
        /// <summary>
        /// The device did not respond within the timeout period
        /// </summary>
        Timeout
    }
    
    /// <summary>
    /// Represents the result of a single device scan operation
    /// </summary>
    public class DeviceScanResult
    {
        /// <summary>
        /// The slave ID that was scanned
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// Whether the device responded
        /// </summary>
        public bool Responded { get; set; }
        
        /// <summary>
        /// The status of the response
        /// </summary>
        public ResponseStatus ResponseStatus { get; set; }

        /// <summary>
        /// Time taken to respond in milliseconds
        /// </summary>
        public double ResponseTime { get; set; }

        /// <summary>
        /// Whether the device responded with a Modbus exception
        /// </summary>
        public bool IsException { get; set; }

        /// <summary>
        /// Exception code if applicable
        /// </summary>
        public byte? ExceptionCode { get; set; }

        /// <summary>
        /// Description of the exception if applicable
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Timestamp when the scan completed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
