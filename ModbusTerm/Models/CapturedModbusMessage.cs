using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Represents a captured Modbus message from the listen service
    /// </summary>
    public class CapturedModbusMessage : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private byte _slaveId;
        private byte _functionCode;
        private MessageDirection _direction;
        private byte[] _rawData = Array.Empty<byte>();
        private bool _crcValid;
        private string _parsedData = string.Empty;
        private int _dataLength;

        /// <summary>
        /// Timestamp when the message was captured
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        /// <summary>
        /// Modbus slave ID
        /// </summary>
        public byte SlaveId
        {
            get => _slaveId;
            set => SetProperty(ref _slaveId, value);
        }

        /// <summary>
        /// Modbus function code
        /// </summary>
        public byte FunctionCode
        {
            get => _functionCode;
            set => SetProperty(ref _functionCode, value);
        }

        /// <summary>
        /// Direction of the message (Master to Slave, Slave to Master, or Unknown)
        /// </summary>
        public MessageDirection Direction
        {
            get => _direction;
            set => SetProperty(ref _direction, value);
        }

        /// <summary>
        /// Raw byte data of the complete frame
        /// </summary>
        public byte[] RawData
        {
            get => _rawData;
            set => SetProperty(ref _rawData, value);
        }

        /// <summary>
        /// Whether the CRC is valid
        /// </summary>
        public bool CrcValid
        {
            get => _crcValid;
            set => SetProperty(ref _crcValid, value);
        }

        /// <summary>
        /// Human-readable parsed data
        /// </summary>
        public string ParsedData
        {
            get => _parsedData;
            set => SetProperty(ref _parsedData, value);
        }

        /// <summary>
        /// Length of the data portion (excluding slave ID, function code, and CRC)
        /// </summary>
        public int DataLength
        {
            get => _dataLength;
            set => SetProperty(ref _dataLength, value);
        }

        /// <summary>
        /// Function code as hex string for display
        /// </summary>
        public string FunctionCodeHex => $"0x{FunctionCode:X2}";

        /// <summary>
        /// Raw data as hex string for display
        /// </summary>
        public string RawDataHex => BitConverter.ToString(RawData);

        /// <summary>
        /// Timestamp formatted for display
        /// </summary>
        public string TimestampFormatted => Timestamp.ToString("HH:mm:ss.fff");

        /// <summary>
        /// CRC status as string for display
        /// </summary>
        public string CrcStatus => CrcValid ? "Valid" : "Invalid";

        /// <summary>
        /// Direction as string for display
        /// </summary>
        public string DirectionString => Direction switch
        {
            MessageDirection.MasterToSlave => "Master → Slave",
            MessageDirection.SlaveToMaster => "Slave → Master",
            MessageDirection.Unknown => "Unknown",
            _ => "Unknown"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Direction of a Modbus message
    /// </summary>
    public enum MessageDirection
    {
        Unknown,
        MasterToSlave,
        SlaveToMaster
    }
}
