using System.Text;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Type of communication event
    /// </summary>
    public enum EventType
    {
        Sent,
        Received,
        Error,
        Info
    }

    /// <summary>
    /// Represents a communication event in the log
    /// </summary>
    public class CommunicationEvent
    {
        /// <summary>
        /// Gets or sets the timestamp of the event
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the type of event
        /// </summary>
        public EventType Type { get; set; }

        /// <summary>
        /// Gets or sets the raw message data as bytes
        /// </summary>
        public byte[]? RawData { get; set; }

        /// <summary>
        /// Gets or sets additional message or error description
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets the event type as string for display purposes
        /// </summary>
        public string TypeString => Type.ToString();

        /// <summary>
        /// Gets the timestamp formatted as string
        /// </summary>
        public string TimestampString => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

        /// <summary>
        /// Gets the raw data formatted as hexadecimal string
        /// </summary>
        public string HexData
        {
            get
            {
                if (RawData == null || RawData.Length == 0)
                    return string.Empty;

                StringBuilder sb = new StringBuilder(RawData.Length * 3);
                foreach (byte b in RawData)
                {
                    sb.Append(b.ToString("X2"));
                    sb.Append(' ');
                }
                return sb.ToString().Trim();
            }
        }

        /// <summary>
        /// Creates a new communication event with the current timestamp
        /// </summary>
        public CommunicationEvent()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Creates a sent event with the specified data
        /// </summary>
        /// <param name="data">The raw data sent</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateSentEvent(byte[] data)
        {
            return new CommunicationEvent
            {
                Type = EventType.Sent,
                RawData = data
            };
        }

        /// <summary>
        /// Creates a sent event with the specified data and message
        /// </summary>
        /// <param name="data">The raw data sent</param>
        /// <param name="message">The message describing what was sent</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateSentEvent(byte[] data, string message)
        {
            return new CommunicationEvent
            {
                Type = EventType.Sent,
                RawData = data,
                Message = message
            };
        }

        /// <summary>
        /// Creates a received event with the specified data
        /// </summary>
        /// <param name="data">The raw data received</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateReceivedEvent(byte[] data)
        {
            return new CommunicationEvent
            {
                Type = EventType.Received,
                RawData = data
            };
        }

        /// <summary>
        /// Creates a received event with the specified data and message
        /// </summary>
        /// <param name="data">The raw data received</param>
        /// <param name="message">The message describing what was received</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateReceivedEvent(byte[] data, string message)
        {
            return new CommunicationEvent
            {
                Type = EventType.Received,
                RawData = data,
                Message = message
            };
        }

        /// <summary>
        /// Creates an error event with the specified message
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="data">Optional raw data related to the error</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateErrorEvent(string message, byte[]? data = null)
        {
            return new CommunicationEvent
            {
                Type = EventType.Error,
                Message = message,
                RawData = data
            };
        }

        /// <summary>
        /// Creates an info event with the specified message
        /// </summary>
        /// <param name="message">The information message</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateInfoEvent(string message)
        {
            return new CommunicationEvent
            {
                Type = EventType.Info,
                Message = message
            };
        }
        
        /// <summary>
        /// Creates a warning event with the specified message
        /// </summary>
        /// <param name="message">The warning message</param>
        /// <returns>A new CommunicationEvent</returns>
        public static CommunicationEvent CreateWarningEvent(string message)
        {
            return new CommunicationEvent
            {
                Type = EventType.Info, // Using Info type since there's no Warning type
                Message = $"⚠️ {message}" // Prefixing with warning emoji
            };
        }
    }
}
