using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ModbusTerm.Models;

namespace ModbusTerm.Services
{
    /// <summary>
    /// Service for listening to Modbus RTU traffic without responding
    /// </summary>
    public class ModbusListenService : IDisposable
    {
        private readonly ConcurrentQueue<byte> _dataBuffer = new();
        private readonly Timer _frameTimer;
        private readonly Dispatcher _dispatcher;
        private readonly object _lockObject = new();
        private bool _isListening = false;
        private SerialPort? _serialPort;
        private CancellationTokenSource? _cancellationTokenSource;
        private DateTime _lastMessageTime = DateTime.MinValue;
        private MessageDirection _lastProcessedDirection = MessageDirection.MasterToSlave;
        private int _currentBaudRate = 9600;

        // Buffer for incoming data
        private int _frameTimeoutMs = 50; // RTU frame timeout (calculated based on baud rate)
        private const int MAX_CAPTURED_MESSAGES = 20000;
        
        /// <summary>
        /// Collection of captured Modbus messages
        /// </summary>
        public ObservableCollection<CapturedModbusMessage> CapturedMessages { get; } = new ObservableCollection<CapturedModbusMessage>();
        
        /// <summary>
        /// Event raised when a communication event occurs
        /// </summary>
        public event EventHandler<CommunicationEvent>? CommunicationEventOccurred;
        
        /// <summary>
        /// Event raised when a new message is captured
        /// </summary>
        public event EventHandler<CapturedModbusMessage>? MessageCaptured;

        public ModbusListenService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _frameTimer = new Timer(ProcessFrame, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Start listening on the specified serial port
        /// </summary>
        public Task<bool> StartListeningAsync(RtuConnectionParameters parameters)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isListening)
                    {
                        return Task.FromResult(false);
                    }

                    // Store baud rate for timing calculations
                    _currentBaudRate = parameters.UseCustomBaudRate ? parameters.CustomBaudRate : parameters.BaudRate;
                    
                    // Calculate frame timeout based on Modbus RTU specification (3.5 character times)
                    // At high baud rates, use much more conservative timeouts
                    double characterTimeMs = (11.0 * 1000.0) / _currentBaudRate; // 11 bits per character (8N1 + start/stop)
                    _frameTimeoutMs = Math.Max((int)(3.5 * characterTimeMs), 2); // Minimum 2ms
                    
                    // For very high baud rates, use much longer timeouts to prevent fragmentation
                    if (_currentBaudRate >= 500000)
                    {
                        _frameTimeoutMs = 100; // 100ms for 500k+ baud to ensure complete frames
                    }
                    else if (_currentBaudRate >= 115200)
                    {
                        _frameTimeoutMs = Math.Max(_frameTimeoutMs, 50); // 50ms for high baud rates
                    }
                    else if (_currentBaudRate >= 57600)
                    {
                        _frameTimeoutMs = Math.Max((int)(3.5 * characterTimeMs), 3); // Minimum 3ms for medium speed
                    }
                    else
                    {
                        _frameTimeoutMs = Math.Max((int)(3.5 * characterTimeMs), 2); // Minimum 2ms for low speed
                    }
                    
                    _serialPort = new SerialPort
                    {
                        PortName = parameters.ComPort,
                        BaudRate = _currentBaudRate,
                        DataBits = parameters.DataBits,
                        Parity = parameters.Parity,
                        StopBits = parameters.StopBits,
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.Open();

                    _cancellationTokenSource = new CancellationTokenSource();
                    _isListening = true;
                }

                OnCommunicationEvent(CommunicationEvent.CreateInfoEvent($"Started listening on {parameters.ComPort}"));
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Failed to start listening: {ex.Message}"));
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Stop listening
        /// </summary>
        public Task StopListeningAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_isListening)
                    {
                        return Task.CompletedTask;
                    }

                    _isListening = false;
                    _cancellationTokenSource?.Cancel();
                    
                    if (_serialPort != null)
                    {
                        _serialPort.DataReceived -= SerialPort_DataReceived;
                        _serialPort.Close();
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                }

                _frameTimer.Change(Timeout.Infinite, Timeout.Infinite);
                OnCommunicationEvent(CommunicationEvent.CreateInfoEvent("Stopped listening"));
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error stopping listen service: {ex.Message}"));
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle incoming serial data
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return;

                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead == 0)
                    return;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

                // Add bytes to buffer
                for (int i = 0; i < bytesRead; i++)
                {
                    _dataBuffer.Enqueue(buffer[i]);
                }

                // For low baud rates, process frames immediately for responsiveness
                // For very high baud rates (500k+), disable immediate processing entirely
                // For medium-high baud rates, use conservative immediate processing
                if (_currentBaudRate < 57600)
                {
                    ProcessAvailableFrames();
                }
                else if (_currentBaudRate < 500000)
                {
                    // For medium-high baud rates, only process if we have a reasonable amount of data
                    if (_dataBuffer.Count >= 8) // At least one complete frame worth
                    {
                        ProcessAvailableFrames();
                    }
                }
                // For 500k+ baud, rely entirely on timer-based processing
                
                // Set timer for frame completion detection
                _frameTimer.Change(_frameTimeoutMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error reading serial data: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Process any complete frames that are available in the buffer
        /// </summary>
        private void ProcessAvailableFrames()
        {
            // Convert buffer to array for analysis
            var bufferData = _dataBuffer.ToArray();
            if (bufferData.Length < 4)
                return;
                
            int processed = 0;
            int offset = 0;
            
            while (offset < bufferData.Length - 3)
            {
                var frame = TryExtractCompleteFrame(bufferData, offset);
                if (frame != null && frame.Length >= 4)
                {
                    // Remove processed bytes from buffer
                    for (int i = 0; i < frame.Length; i++)
                    {
                        _dataBuffer.TryDequeue(out _);
                    }
                    
                    // Process the frame
                    ProcessSingleFrame(frame);
                    processed += frame.Length;
                    offset = 0; // Start over with remaining data
                    bufferData = _dataBuffer.ToArray();
                }
                else
                {
                    break; // No more complete frames
                }
            }
        }
        
        /// <summary>
        /// Try to extract a complete frame starting at the beginning of the data
        /// </summary>
        private byte[]? TryExtractCompleteFrame(byte[] data, int startOffset)
        {
            if (startOffset + 3 >= data.Length)
                return null;
                
            byte slaveId = data[startOffset];
            byte functionCode = data[startOffset + 1];
            
            // Calculate expected frame length
            int expectedLength = GetExpectedFrameLength(functionCode, data, startOffset);
            
            if (expectedLength > 0 && startOffset + expectedLength <= data.Length)
            {
                byte[] frame = new byte[expectedLength];
                Array.Copy(data, startOffset, frame, 0, expectedLength);
                
                // Always return the frame - CRC validation will be shown in UI
                // The ParseModbusFrame method will handle CRC validation for display
                return frame;
            }
            
            return null;
        }
        
        /// <summary>
        /// Process a single complete frame
        /// </summary>
        private void ProcessSingleFrame(byte[] frameBytes)
        {
            var capturedMessage = ParseModbusFrame(frameBytes);
            if (capturedMessage != null)
            {
                _dispatcher.Invoke(() =>
                {
                    // Maintain buffer size limit
                    while (CapturedMessages.Count >= MAX_CAPTURED_MESSAGES)
                    {
                        CapturedMessages.RemoveAt(0);
                    }

                    CapturedMessages.Add(capturedMessage);
                    MessageCaptured?.Invoke(this, capturedMessage);
                });
            }
        }
        
        /// <summary>
        /// Process a single complete frame with predetermined direction (for hybrid algorithm)
        /// </summary>
        private void ProcessSingleFrameWithDirection(byte[] frameBytes, MessageDirection direction)
        {
            var capturedMessage = ParseModbusFrameWithDirection(frameBytes, direction);
            if (capturedMessage != null)
            {
                _dispatcher.Invoke(() =>
                {
                    // Maintain buffer size limit
                    while (CapturedMessages.Count >= MAX_CAPTURED_MESSAGES)
                    {
                        CapturedMessages.RemoveAt(0);
                    }

                    CapturedMessages.Add(capturedMessage);
                    MessageCaptured?.Invoke(this, capturedMessage);
                });
            }
        }

        /// <summary>
        /// Process accumulated frame data after timeout
        /// </summary>
        private void ProcessFrame(object? state)
        {
            if (_dataBuffer.IsEmpty)
                return;

            try
            {
                // For high baud rates, use a more conservative approach
                if (_currentBaudRate >= 115200)
                {
                    ProcessFramesConservative();
                }
                else
                {
                    // Use the aggressive approach for lower baud rates
                    ProcessAvailableFrames();
                }
                
                // Clean up any remaining invalid data
                CleanupRemainingData();
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error processing frame: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Hybrid frame processing for high baud rates using timing, CRC, and Master/Slave alternation
        /// </summary>
        private void ProcessFramesConservative()
        {
            var allData = _dataBuffer.ToArray();
            if (allData.Length < 4)
                return;
                
            // Clear the buffer first
            while (_dataBuffer.TryDequeue(out _)) { }
            
            // Use hybrid approach for high-speed frame detection
            if (_currentBaudRate >= 500000)
            {
                ProcessFramesHybrid(allData);
            }
            else
            {
                // Use simpler approach for medium baud rates
                ProcessFramesSimple(allData);
            }
        }
        
        /// <summary>
        /// Hybrid frame processing combining timing, CRC validation, and Master/Slave alternation
        /// </summary>
        private void ProcessFramesHybrid(byte[] allData)
        {
            var candidateFrames = new List<CandidateFrame>();
            
            // Find all potential frame candidates with valid CRC
            for (int offset = 0; offset <= allData.Length - 5; offset++)
            {
                // Try different frame lengths starting from minimum (5 bytes)
                for (int len = 5; len <= Math.Min(256, allData.Length - offset); len++)
                {
                    if (offset + len <= allData.Length)
                    {
                        byte[] testFrame = new byte[len];
                        Array.Copy(allData, offset, testFrame, 0, len);
                        
                        // Only consider frames with valid structure and CRC
                        if (IsValidFrameStructure(testFrame) && VerifyModbusCrcSilent(testFrame))
                        {
                            // For high-speed traffic, use structure-based direction detection
                            // as timing-based detection is unreliable at 500k+ baud
                            var direction = DetermineFrameDirectionByStructure(testFrame);
                            candidateFrames.Add(new CandidateFrame
                            {
                                Offset = offset,
                                Length = len,
                                Data = testFrame,
                                Direction = direction,
                                SlaveId = testFrame[0],
                                FunctionCode = testFrame[1]
                            });
                        }
                    }
                }
            }
            
            // Apply timing-based direction detection: first frame = master, quick follow-up = slave, long gap = new master
            ApplyTimingBasedDirectionDetection(candidateFrames);
            
            // Select best frame sequence using Master/Slave alternation logic
            var selectedFrames = SelectOptimalFrameSequence(candidateFrames);
            
            // Update last processed direction for next batch
            if (selectedFrames.Count > 0)
            {
                _lastProcessedDirection = selectedFrames.Last().Direction;
            }
            
            // Process selected frames with their determined directions
            foreach (var frame in selectedFrames)
            {
                ProcessSingleFrameWithDirection(frame.Data, frame.Direction);
            }
        }
        
        /// <summary>
        /// Simple frame processing for medium baud rates
        /// </summary>
        private void ProcessFramesSimple(byte[] allData)
        {
            int offset = 0;
            while (offset < allData.Length - 3)
            {
                var frame = FindValidFrame(allData, offset);
                if (frame != null && frame.Length >= 4)
                {
                    ProcessSingleFrame(frame);
                    offset += frame.Length;
                }
                else
                {
                    // Skip invalid byte
                    offset++;
                }
            }
        }
        
        /// <summary>
        /// Candidate frame for hybrid processing
        /// </summary>
        private class CandidateFrame
        {
            public int Offset { get; set; }
            public int Length { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public MessageDirection Direction { get; set; }
            public byte SlaveId { get; set; }
            public byte FunctionCode { get; set; }
        }
        
        /// <summary>
        /// Select optimal frame sequence using Master/Slave alternation and non-overlapping logic
        /// </summary>
        private List<CandidateFrame> SelectOptimalFrameSequence(List<CandidateFrame> candidates)
        {
            if (candidates.Count == 0)
                return new List<CandidateFrame>();
                
            // Sort candidates by offset
            candidates.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            
            var selectedFrames = new List<CandidateFrame>();
            int lastEndOffset = 0;
            
            foreach (var candidate in candidates)
            {
                // Skip overlapping frames
                if (candidate.Offset < lastEndOffset)
                    continue;
                    
                // For high-speed traffic, be more permissive to ensure we capture both master and slave frames
                bool shouldSelect = true; // Default to selecting all non-overlapping frames
                
                // Only apply strict alternation if we have a clear pattern
                if (selectedFrames.Count > 0 && _currentBaudRate >= 500000)
                {
                    var lastFrame = selectedFrames.Last();
                    var gap = candidate.Offset - (lastFrame.Offset + lastFrame.Length);
                    
                    // If frames are very close together (< 5 bytes gap), prefer alternation
                    if (gap < 5)
                    {
                        if (lastFrame.Direction == candidate.Direction)
                        {
                            // Same direction in quick succession - might be fragmentation
                            // Only skip if the frame lengths suggest it's a fragment
                            if (candidate.Length < 8 && lastFrame.Length < 8)
                            {
                                shouldSelect = false;
                            }
                        }
                    }
                }
                
                if (shouldSelect)
                {
                    selectedFrames.Add(candidate);
                    lastEndOffset = candidate.Offset + candidate.Length;
                }
            }
            
            return selectedFrames;
        }
        
        /// <summary>
        /// Determine frame direction based on structure with alternation preference for ambiguous cases
        /// </summary>
        private MessageDirection DetermineFrameDirectionByStructure(byte[] frameBytes)
        {
            if (frameBytes.Length < 2)
                return MessageDirection.MasterToSlave;
                
            byte functionCode = frameBytes[1];
            
            // Exception responses are always Slave to Master
            if ((functionCode & 0x80) != 0)
            {
                return MessageDirection.SlaveToMaster;
            }
            
            // For read functions, use frame length and structure to distinguish request vs response
            switch (functionCode)
            {
                case 0x01: // Read Coils
                case 0x02: // Read Discrete Inputs
                case 0x03: // Read Holding Registers  
                case 0x04: // Read Input Registers
                    if (frameBytes.Length == 8)
                    {
                        // 8-byte frame: SlaveID + FC + StartAddr(2) + Quantity(2) + CRC(2) = Request
                        return MessageDirection.MasterToSlave;
                    }
                    else if (frameBytes.Length > 8 && frameBytes.Length >= 5)
                    {
                        // Variable length frame: SlaveID + FC + ByteCount + Data + CRC(2) = Response
                        // Verify it has a reasonable byte count field
                        if (frameBytes.Length >= 5)
                        {
                            byte byteCount = frameBytes[2];
                            int expectedLength = 3 + byteCount + 2; // SlaveID + FC + ByteCount + Data + CRC
                            if (frameBytes.Length == expectedLength)
                            {
                                return MessageDirection.SlaveToMaster;
                            }
                        }
                        // If structure doesn't match response format, assume it's a request
                        return MessageDirection.MasterToSlave;
                    }
                    else
                    {
                        // Short frame, probably incomplete - assume request
                        return MessageDirection.MasterToSlave;
                    }
                    
                case 0x05: // Write Single Coil
                case 0x06: // Write Single Register
                    // Both request and response are 8 bytes for these functions - AMBIGUOUS
                    // Return special marker to indicate alternation preference needed
                    return MessageDirection.MasterToSlave; // Will be corrected by alternation logic
                    
                case 0x0F: // Write Multiple Coils
                case 0x10: // Write Multiple Registers
                    if (frameBytes.Length == 8)
                    {
                        // 8-byte response: SlaveID + FC + StartAddr(2) + Quantity(2) + CRC(2)
                        return MessageDirection.SlaveToMaster;
                    }
                    else
                    {
                        // Longer frame: SlaveID + FC + StartAddr(2) + Quantity(2) + ByteCount + Data + CRC(2) = Request
                        return MessageDirection.MasterToSlave;
                    }
                    
                default:
                    // Unknown function code, assume master request
                    return MessageDirection.MasterToSlave;
            }
        }
        
        /// <summary>
        /// Apply timing-based direction detection: first frame = master, quick follow-up = slave, long gap = new master
        /// </summary>
        private void ApplyTimingBasedDirectionDetection(List<CandidateFrame> candidates)
        {
            if (candidates.Count == 0)
                return;
                
            // Calculate response threshold based on baud rate
            double responseThresholdMs = CalculateResponseThreshold(_currentBaudRate);
                
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var originalDirection = candidate.Direction;
                MessageDirection newDirection;
                
                if (i == 0)
                {
                    // First frame: assume master request and validate structure
                    newDirection = MessageDirection.MasterToSlave;
                    
                    // Sanity check: does this look like a master request?
                    bool looksLikeMasterRequest = IsLikelyMasterRequest(candidate);
                    // Note: We proceed with master assumption even if structure looks unusual
                }
                else
                {
                    // Determine direction based on Modbus communication pattern
                    var prevCandidate = candidates[i - 1];
                    
                    if (prevCandidate.Direction == MessageDirection.MasterToSlave)
                    {
                        // Previous was master request: this should be slave response (if timing is quick)
                        int gapBytes = candidate.Offset - (prevCandidate.Offset + prevCandidate.Length);
                        double estimatedGapMs = (gapBytes * 11.0 * 1000.0) / _currentBaudRate;
                        
                        if (estimatedGapMs <= responseThresholdMs)
                        {
                            // Quick follow-up after master request = slave response
                            newDirection = MessageDirection.SlaveToMaster;
                        }
                        else
                        {
                            // Long gap after master request = new master request (no response received)
                            newDirection = MessageDirection.MasterToSlave;
                        }
                    }
                    else
                    {
                        // Previous was slave response: next frame is ALWAYS a new master request
                        // (Modbus protocol: master request → slave response → master request)
                        newDirection = MessageDirection.MasterToSlave;
                    }
                }
                
                if (newDirection != originalDirection)
                {
                    candidate.Direction = newDirection;
                }
            }
        }
        
        /// <summary>
        /// Check if a frame looks like a typical master request
        /// </summary>
        private bool IsLikelyMasterRequest(CandidateFrame candidate)
        {
            // Most master requests are 8 bytes (except write multiple)
            // Exception responses are never master requests
            if ((candidate.FunctionCode & 0x80) != 0)
                return false; // Exception response
                
            switch (candidate.FunctionCode)
            {
                case 0x01: case 0x02: case 0x03: case 0x04: // Read functions
                case 0x05: case 0x06: // Write single
                    return candidate.Length == 8;
                    
                case 0x0F: case 0x10: // Write multiple
                    return candidate.Length > 8; // Requests are longer, responses are 8 bytes
                    
                default:
                    return candidate.Length == 8; // Most requests are 8 bytes
            }
        }
        
        /// <summary>
        /// Legacy method - kept for compatibility with timing-based detection
        /// </summary>
        private MessageDirection DetermineFrameDirection(byte[] frameBytes)
        {
            return DetermineFrameDirectionByStructure(frameBytes);
        }

        /// <summary>
        /// Find a valid frame starting from the given offset
        /// </summary>
        private byte[]? FindValidFrame(byte[] data, int offset)
        {
            if (offset + 3 >= data.Length)
                return null;
                
            // Try different frame lengths and validate with CRC
            for (int len = 5; len <= Math.Min(256, data.Length - offset); len++)
            {
                if (offset + len <= data.Length)
                {
                    byte[] testFrame = new byte[len];
                    Array.Copy(data, offset, testFrame, 0, len);
                    
                    // Check if this has valid Modbus structure
                    // Allow frames with invalid CRC to be processed for display
                    if (IsValidFrameStructure(testFrame))
                    {
                        return testFrame;
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Check if frame has valid Modbus structure
        /// </summary>
        private bool IsValidFrameStructure(byte[] frame)
        {
            if (frame.Length < 4)
                return false;
                
            byte slaveId = frame[0];
            byte functionCode = frame[1];
            
            // Slave ID should be 1-247 or 0 for broadcast
            if (slaveId > 247)
                return false;
                
            // Check function code validity
            if ((functionCode & 0x80) != 0)
            {
                // Exception response - should be 5 bytes
                return frame.Length == 5;
            }
            else
            {
                // Normal function codes
                switch (functionCode)
                {
                    case 0x01:
                    case 0x02:
                    case 0x03:
                    case 0x04:
                    case 0x05:
                    case 0x06:
                        return true; // These are valid function codes
                    case 0x0F:
                    case 0x10:
                        return true; // These are valid function codes
                    default:
                        return false; // Invalid function code
                }
            }
        }
        
        /// <summary>
        /// Clean up any remaining invalid data
        /// </summary>
        private void CleanupRemainingData()
        {
            if (!_dataBuffer.IsEmpty)
            {
                var remainingBytes = new List<byte>();
                while (_dataBuffer.TryDequeue(out byte b))
                {
                    remainingBytes.Add(b);
                }
                
                if (remainingBytes.Count > 0)
                {
                    string hexData = string.Join("-", remainingBytes.Select(b => b.ToString("X2")));
                    OnCommunicationEvent(CommunicationEvent.CreateWarningEvent($"Discarded invalid data: {hexData}"));
                }
            }
        }



        /// <summary>
        /// Get expected frame length based on function code and data
        /// </summary>
        private int GetExpectedFrameLength(byte functionCode, byte[] data, int offset)
        {
            if (offset + 3 >= data.Length)
                return 0;

            try
            {
                // Handle exception responses first (function code with high bit set)
                if ((functionCode & 0x80) != 0)
                {
                    // Exception response: SlaveID + ExceptionFC + ExceptionCode + CRC(2) = 5 bytes
                    return 5;
                }
                
                switch (functionCode)
                {
                    case 0x01: // Read Coils
                    case 0x02: // Read Discrete Inputs
                    case 0x03: // Read Holding Registers
                    case 0x04: // Read Input Registers
                        // Need to distinguish between request and response frames
                        // Request: SlaveID + FC + StartAddr(2) + Quantity(2) + CRC(2) = 8 bytes
                        // Response: SlaveID + FC + ByteCount + Data + CRC(2) = variable length
                        
                        if (offset + 7 < data.Length)
                        {
                            // Try as 8-byte request first - check if it has valid structure
                            // For read functions, bytes 2-3 are start address, bytes 4-5 are quantity
                            // Quantity should be reasonable (1-125 for registers, 1-2000 for coils)
                            ushort quantity = (ushort)((data[offset + 4] << 8) | data[offset + 5]);
                            if (quantity > 0 && quantity <= 125) // Reasonable register quantity
                            {
                                return 8; // Request frame
                            }
                        }
                        
                        // Try as response frame - byte 2 is byte count
                        if (offset + 2 < data.Length)
                        {
                            byte byteCount = data[offset + 2];
                            if (byteCount > 0 && byteCount <= 250 && offset + 2 + byteCount + 2 <= data.Length)
                            {
                                return 3 + byteCount + 2; // SlaveID + FC + ByteCount + Data + CRC
                            }
                        }
                        
                        // Default to request frame if unclear
                        return 8;
                        
                    case 0x05: // Write Single Coil
                    case 0x06: // Write Single Register
                        return 8; // SlaveID + FC + Address(2) + Value(2) + CRC(2)
                        
                    case 0x0F: // Write Multiple Coils
                    case 0x10: // Write Multiple Registers
                        if (offset + 6 < data.Length)
                        {
                            byte byteCount = data[offset + 6];
                            return 7 + byteCount + 2; // SlaveID + FC + Address(2) + Count(2) + ByteCount + Data + CRC(2)
                        }
                        return 0;
                        
                    default:
                        return 8; // Default frame size
                }
            }
            catch
            {
                return 8; // Default on any error
            }
        }

        /// <summary>
        /// Parse a Modbus RTU frame
        /// </summary>
        private CapturedModbusMessage? ParseModbusFrame(byte[] frameBytes)
        {
            if (frameBytes.Length < 4)
                return null;

            try
            {
                byte slaveId = frameBytes[0];
                byte functionCode = frameBytes[1];
                
                // Verify CRC if frame is long enough
                bool crcValid = false;
                if (frameBytes.Length >= 4)
                {
                    crcValid = VerifyModbusCrc(frameBytes);
                }

                // Determine message direction based on function code and content
                var direction = DetermineMessageDirection(functionCode, frameBytes);
                
                // Parse data based on function code
                var parsedData = ParseFunctionData(functionCode, frameBytes, direction);

                return new CapturedModbusMessage
                {
                    Timestamp = DateTime.Now,
                    SlaveId = slaveId,
                    FunctionCode = functionCode,
                    Direction = direction,
                    RawData = frameBytes,
                    CrcValid = crcValid,
                    ParsedData = parsedData,
                    DataLength = frameBytes.Length - 4 // Exclude slave ID, function code, and CRC
                };
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error parsing frame: {ex.Message}"));
                return null;
            }
        }
        
        /// <summary>
        /// Parse a Modbus RTU frame with predetermined direction (for hybrid algorithm)
        /// </summary>
        private CapturedModbusMessage? ParseModbusFrameWithDirection(byte[] frameBytes, MessageDirection direction)
        {
            if (frameBytes.Length < 4)
                return null;

            try
            {
                byte slaveId = frameBytes[0];
                byte functionCode = frameBytes[1];
                
                // Verify CRC if frame is long enough
                bool crcValid = false;
                if (frameBytes.Length >= 4)
                {
                    crcValid = VerifyModbusCrc(frameBytes);
                }

                // Use the predetermined direction from hybrid algorithm
                // Parse data based on function code
                var parsedData = ParseFunctionData(functionCode, frameBytes, direction);

                return new CapturedModbusMessage
                {
                    Timestamp = DateTime.Now,
                    SlaveId = slaveId,
                    FunctionCode = functionCode,
                    Direction = direction, // Use the predetermined direction
                    RawData = frameBytes,
                    CrcValid = crcValid,
                    ParsedData = parsedData,
                    DataLength = frameBytes.Length - 4 // Exclude slave ID, function code, and CRC
                };
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error parsing frame: {ex.Message}"));
                return null;
            }
        }

        /// <summary>
        /// Determine if message is from master or slave based on timing
        /// </summary>
        private MessageDirection DetermineMessageDirection(byte functionCode, byte[] frameBytes)
        {
            DateTime currentTime = DateTime.Now;
            
            // Handle exception responses (function code with high bit set)
            if ((functionCode & 0x80) != 0)
            {
                return MessageDirection.SlaveToMaster;
            }

            // Calculate timing thresholds based on baud rate
            // At higher baud rates, responses come faster
            double responseThresholdMs = CalculateResponseThreshold(_currentBaudRate);
            double masterIntervalMs = CalculateMasterInterval(_currentBaudRate);
            
            // Calculate time since last message
            double timeSinceLastMs = (_lastMessageTime == DateTime.MinValue) ? 
                double.MaxValue : (currentTime - _lastMessageTime).TotalMilliseconds;
            
            // Update last message time for next calculation
            _lastMessageTime = currentTime;
            
            // Timing-based direction detection:
            // - Master messages come after longer intervals (master polling cycle)
            // - Slave responses come quickly after master requests (within response threshold)
            if (timeSinceLastMs <= responseThresholdMs)
            {
                // Quick response = Slave to Master
                return MessageDirection.SlaveToMaster;
            }
            else if (timeSinceLastMs >= masterIntervalMs)
            {
                // Longer interval = Master to Slave (new request)
                return MessageDirection.MasterToSlave;
            }
            else
            {
                // Ambiguous timing - fall back to frame structure analysis
                return DetermineDirectionByStructure(functionCode, frameBytes);
            }
        }
        
        /// <summary>
        /// Calculate response threshold based on baud rate
        /// Slave responses typically come within 1-50ms depending on baud rate
        /// </summary>
        private double CalculateResponseThreshold(int baudRate)
        {
            // Calculate based on frame transmission time
            // Typical slave response time: 1-10ms + frame transmission time
            double characterTimeMs = (11.0 * 1000.0) / Math.Max(baudRate, 1200);
            double maxFrameTimeMs = characterTimeMs * 256; // Max frame size
            double responseTimeMs = 10.0 + maxFrameTimeMs; // Processing + transmission
            
            // Exception responses are typically faster than normal responses
            // Increase threshold to ensure we catch fast exception responses
            double threshold = Math.Max(responseTimeMs * 1.5, 10.0); // 1.5x normal + minimum 10ms
            
            // For 9600 baud: ~60ms threshold
            // For 500000 baud: ~16ms threshold
            return threshold;
        }
        
        /// <summary>
        /// Calculate master interval threshold based on baud rate
        /// Master typically waits longer between requests (polling cycle)
        /// </summary>
        private double CalculateMasterInterval(int baudRate)
        {
            // Master intervals are typically much longer than response times
            // Typical polling cycles: 100ms to several seconds
            double responseThreshold = CalculateResponseThreshold(baudRate);
            
            // Use a more conservative threshold - at least 5x response time or 200ms
            // This helps distinguish between quick slave responses and new master requests
            return Math.Max(responseThreshold * 5.0, 200.0);
        }
        
        /// <summary>
        /// Fallback direction detection based on frame structure
        /// </summary>
        private MessageDirection DetermineDirectionByStructure(byte functionCode, byte[] frameBytes)
        {
            if (frameBytes.Length >= 4)
            {
                switch (functionCode)
                {
                    case 0x01: // Read Coils
                    case 0x02: // Read Discrete Inputs
                    case 0x03: // Read Holding Registers
                    case 0x04: // Read Input Registers
                        // 8 bytes = request, variable length = response
                        return frameBytes.Length == 8 ? MessageDirection.MasterToSlave : MessageDirection.SlaveToMaster;
                    
                    case 0x05: // Write Single Coil
                    case 0x06: // Write Single Register
                        // Both are 8 bytes - default to master request
                        return MessageDirection.MasterToSlave;
                    
                    case 0x0F: // Write Multiple Coils
                    case 0x10: // Write Multiple Registers
                        // Longer frames = request, 8 bytes = response
                        return frameBytes.Length > 8 ? MessageDirection.MasterToSlave : MessageDirection.SlaveToMaster;
                    
                    default:
                        return MessageDirection.Unknown;
                }
            }

            return MessageDirection.Unknown;
        }

        /// <summary>
        /// Parse function-specific data
        /// </summary>
        private string ParseFunctionData(byte functionCode, byte[] frameBytes, MessageDirection direction)
        {
            try
            {
                // Handle exception responses
                if ((functionCode & 0x80) != 0)
                {
                    byte originalFunction = (byte)(functionCode & 0x7F);
                    byte exceptionCode = frameBytes.Length > 2 ? frameBytes[2] : (byte)0;
                    return $"Exception Response - Original Function: {originalFunction:X2}, Exception Code: {exceptionCode:X2}";
                }

                switch (functionCode)
                {
                    case 0x01: // Read Coils
                    case 0x02: // Read Discrete Inputs
                        if (direction == MessageDirection.MasterToSlave && frameBytes.Length >= 6)
                        {
                            ushort startAddress = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort quantity = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            return $"Start Address: {startAddress}, Quantity: {quantity}";
                        }
                        else if (direction == MessageDirection.SlaveToMaster && frameBytes.Length >= 3)
                        {
                            byte byteCount = frameBytes[2];
                            return $"Byte Count: {byteCount}, Data: {BitConverter.ToString(frameBytes, 3, Math.Min(byteCount, frameBytes.Length - 5))}";
                        }
                        break;

                    case 0x03: // Read Holding Registers
                    case 0x04: // Read Input Registers
                        if (direction == MessageDirection.MasterToSlave && frameBytes.Length >= 6)
                        {
                            ushort startAddress = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort quantity = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            return $"Start Address: {startAddress}, Quantity: {quantity}";
                        }
                        else if (direction == MessageDirection.SlaveToMaster && frameBytes.Length >= 3)
                        {
                            byte byteCount = frameBytes[2];
                            return $"Byte Count: {byteCount}, Data: {BitConverter.ToString(frameBytes, 3, Math.Min(byteCount, frameBytes.Length - 5))}";
                        }
                        break;

                    case 0x05: // Write Single Coil
                        if (frameBytes.Length >= 6)
                        {
                            ushort address = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort value = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            return $"Address: {address}, Value: {(value == 0xFF00 ? "ON" : "OFF")} ({value:X4})";
                        }
                        break;

                    case 0x06: // Write Single Register
                        if (frameBytes.Length >= 6)
                        {
                            ushort address = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort value = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            return $"Address: {address}, Value: {value}";
                        }
                        break;

                    case 0x0F: // Write Multiple Coils
                        if (direction == MessageDirection.MasterToSlave && frameBytes.Length >= 7)
                        {
                            ushort startAddress = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort quantity = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            byte byteCount = frameBytes[6];
                            return $"Start Address: {startAddress}, Quantity: {quantity}, Byte Count: {byteCount}";
                        }
                        else if (direction == MessageDirection.SlaveToMaster && frameBytes.Length >= 6)
                        {
                            ushort startAddress = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort quantity = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            return $"Start Address: {startAddress}, Quantity: {quantity}";
                        }
                        break;

                    case 0x10: // Write Multiple Registers
                        if (direction == MessageDirection.MasterToSlave && frameBytes.Length >= 7)
                        {
                            ushort startAddress = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort quantity = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            byte byteCount = frameBytes[6];
                            return $"Start Address: {startAddress}, Quantity: {quantity}, Byte Count: {byteCount}";
                        }
                        else if (direction == MessageDirection.SlaveToMaster && frameBytes.Length >= 6)
                        {
                            ushort startAddress = (ushort)((frameBytes[2] << 8) | frameBytes[3]);
                            ushort quantity = (ushort)((frameBytes[4] << 8) | frameBytes[5]);
                            return $"Start Address: {startAddress}, Quantity: {quantity}";
                        }
                        break;
                }

                return $"Raw Data: {BitConverter.ToString(frameBytes, 2, frameBytes.Length - 4)}";
            }
            catch
            {
                return "Parse Error";
            }
        }

        /// <summary>
        /// Verify Modbus RTU CRC
        /// </summary>
        private bool VerifyModbusCrc(byte[] frame)
        {
            if (frame.Length < 4)
                return false;

            try
            {
                // CRC is stored in little-endian format (low byte first)
                ushort receivedCrc = (ushort)(frame[frame.Length - 2] | (frame[frame.Length - 1] << 8));
                ushort calculatedCrc = CalculateModbusCrc(frame, frame.Length - 2);
                
                bool isValid = receivedCrc == calculatedCrc;
                
                if (!isValid)
                {
                    string frameHex = string.Join("-", frame.Select(b => b.ToString("X2")));
                    OnCommunicationEvent(CommunicationEvent.CreateInfoEvent(
                        $"CRC mismatch: Frame={frameHex}, Received=0x{receivedCrc:X4}, Calculated=0x{calculatedCrc:X4}"));
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                OnCommunicationEvent(CommunicationEvent.CreateErrorEvent($"Error verifying CRC: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Verify Modbus RTU CRC without logging (for candidate frame testing)
        /// </summary>
        private bool VerifyModbusCrcSilent(byte[] frame)
        {
            if (frame.Length < 4)
                return false;

            try
            {
                // CRC is stored in little-endian format (low byte first)
                ushort receivedCrc = (ushort)(frame[frame.Length - 2] | (frame[frame.Length - 1] << 8));
                ushort calculatedCrc = CalculateModbusCrc(frame, frame.Length - 2);
                
                return receivedCrc == calculatedCrc;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Calculate Modbus RTU CRC
        /// </summary>
        private ushort CalculateModbusCrc(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }

        /// <summary>
        /// Clear all captured messages
        /// </summary>
        public void ClearCapturedMessages()
        {
            _dispatcher.Invoke(() => CapturedMessages.Clear());
            OnCommunicationEvent(CommunicationEvent.CreateInfoEvent("Cleared all captured messages"));
        }

        /// <summary>
        /// Get captured messages as text for export
        /// </summary>
        public string GetCapturedMessagesAsText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp\tDirection\tSlave ID\tFunction Code\tData Length\tCRC Valid\tParsed Data\tRaw Data");
            
            foreach (var message in CapturedMessages)
            {
                sb.AppendLine($"{message.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t" +
                             $"{message.Direction}\t" +
                             $"{message.SlaveId}\t" +
                             $"0x{message.FunctionCode:X2}\t" +
                             $"{message.DataLength}\t" +
                             $"{message.CrcValid}\t" +
                             $"{message.ParsedData}\t" +
                             $"{BitConverter.ToString(message.RawData)}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Get captured messages as CSV format for export
        /// </summary>
        public string GetCapturedMessagesAsCsv()
        {
            var sb = new System.Text.StringBuilder();
            
            // CSV header
            sb.AppendLine("\"Timestamp\",\"Direction\",\"Slave ID\",\"Function Code\",\"Data Length\",\"CRC Valid\",\"Parsed Data\",\"Raw Data\"");
            
            foreach (var message in CapturedMessages)
            {
                // Escape quotes in data fields and wrap in quotes
                var timestamp = EscapeCsvField(message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                var direction = EscapeCsvField(message.Direction.ToString());
                var slaveId = EscapeCsvField(message.SlaveId.ToString());
                var functionCode = EscapeCsvField($"0x{message.FunctionCode:X2}");
                var dataLength = EscapeCsvField(message.DataLength.ToString());
                var crcValid = EscapeCsvField(message.CrcValid.ToString());
                var parsedData = EscapeCsvField(message.ParsedData ?? "");
                var rawData = EscapeCsvField(BitConverter.ToString(message.RawData));
                
                sb.AppendLine($"{timestamp},{direction},{slaveId},{functionCode},{dataLength},{crcValid},{parsedData},{rawData}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Escape a field for CSV format by wrapping in quotes and escaping internal quotes
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";
                
            // Escape quotes by doubling them and wrap the entire field in quotes
            var escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// Raise communication event
        /// </summary>
        private void OnCommunicationEvent(CommunicationEvent eventArgs)
        {
            CommunicationEventOccurred?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Gets whether the service is currently listening
        /// </summary>
        public bool IsListening
        {
            get
            {
                lock (_lockObject)
                {
                    return _isListening;
                }
            }
        }

        public void Dispose()
        {
            _frameTimer?.Dispose();
            _ = StopListeningAsync();
            _cancellationTokenSource?.Dispose();
        }
    }
}
