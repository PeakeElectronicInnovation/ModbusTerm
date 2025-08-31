# Modbus Toolbox

A comprehensive Modbus testing application supporting both TCP and RTU connections with master and slave device functionality. ModbusTerm provides an easy-to-use interface for testing, debugging, and simulating Modbus devices in industrial automation environments.

## Version 1.3.0

Released on 2025-08-31 (see [Releases](https://github.com/PeakeElectronicInnovation/ModbusTerm/releases)). This version introduces advanced data visualization and automation capabilities.

### What's New in Version 1.3

- **Real-time Data Charting**: New floating chart window for visualizing Modbus data in real-time with professional axes, grid lines, and auto-scaling
- **Continuous Request Mode**: Automated continuous polling of Modbus devices with configurable intervals for master mode operations
- **Smart Data Filtering**: Chart automatically filters and converts data types for optimal visualization (excludes ASCII strings, converts multi-register values)
- **Chart Controls**: Pause/resume, clear chart, and configurable maximum points with data decimation for optimal performance

### Previous Version 1.2.0 Features

- **Listen In Mode**: New passive monitoring mode for RTU communications - capture and analyze Modbus traffic between master and slave devices without interfering with communication
- **Enhanced CSV Export**: Listen In data can now be exported in CSV format for analysis in Excel and other spreadsheet applications
- **TCP Slave Connection Monitoring**: Real-time visual feedback when Modbus TCP masters connect to your slave device
- **Smart LED Indicators**: Enhanced connection status display with blue LED when masters are connected to TCP slave
- **Master Connection Logging**: Automatic logging of master connections and disconnections with IP address and hostname resolution
- **Improved Data Capture**: Comprehensive message capture with timestamps, direction, CRC validation, and parsed data

### Previous Version 1.1.0 Features

- **Custom Baud Rates**: Full support for custom baud rate configuration in RTU mode
- **Enhanced ASCII Mode**: Proper ASCII data type implementation with byte swap options (MSB/LSB first)
- **Improved HEX Mode**: Complete HEX mode implementation for better data visualization
- **Slave Mode Fixes**: Resolved register configuration issues when adding registers after connection
- **Slave ID Configuration**: Added slave ID setting capability for slave mode operations
- **Smart Scan Management**: Automatic scan termination on disconnect for better resource management
- **Stability Improvements**: Enhanced serial port handling and communication reliability

## Features

- **Triple Operation Modes**: Master, Slave, and Listen In modes for comprehensive Modbus testing and monitoring
- **Dual Connection Types**: Support for both Modbus TCP (network-based) and Modbus RTU (serial-based) connections
- **Listen In Mode**: Passive monitoring of RTU communications between existing master/slave devices without interference
- **Enhanced Connection Monitoring**: Real-time visual feedback for TCP slave connections with smart LED indicators
- **Connection Profiles**: Save and load connection settings with profile management
- **Real-time Data Charting**: Professional floating chart window with auto-scaling axes, grid lines, and support for 100,000+ data points
- **Data Visualization**: View and interact with Modbus registers in various data formats (UInt16, Int16, UInt32, Int32, Float32, Float64)
- **Byte Order Options**: Support for standard Modbus LSB-first order and optional MSB-first order for compatibility with different devices
- **Real-time Monitoring**: Track communication events through an integrated event log with master connection details
- **Data Export**: Export captured Listen In data in CSV format for analysis in spreadsheet applications
- **Register Management**: Define and manage custom register configurations
- **Device Scanning**: Scan for RTU devices on the network to detect active slave IDs

## Screenshots

### TCP Master Mode
![TCP Master Mode](Images/MBT-TCP-master.png)
*TCP Master mode for sending requests to Modbus TCP servers*

### TCP Slave Mode
![TCP Slave Mode](Images/MBT-TCP-slave.png)
*TCP Slave mode for simulating a Modbus TCP server with configurable registers*

### RTU Master Mode
![RTU Master Mode](Images/MBT-RTU-master.png)
*RTU Master mode for communicating with serial Modbus devices*

### RTU Slave Mode
![RTU Slave Mode](Images/MBT-RTU-slave.png)
*RTU Slave mode for simulating a Modbus RTU device over serial connection*

## Connection Options

### TCP Mode
- IP Address/Hostname configuration
- Custom port settings (default: 502)
- TCP Master and TCP Slave functionality
- Slave ID configuration

### RTU Mode
- COM port selection and configuration
- Adjustable baud rate, parity, data bits, and stop bits
- RTU Master and RTU Slave functionality
- Device scanning capability
- Full support for custom baud rates

## Master Mode Features

- Support for all standard Modbus functions:
  - Read Coils (01)
  - Read Discrete Inputs (02)
  - Read Holding Registers (03)
  - Read Input Registers (04)
  - Write Single Coil (05)
  - Write Single Register (06)
  - Write Multiple Coils (15)
  - Write Multiple Registers (16)
- **Continuous Request Mode**: Automated polling with configurable intervals (minimum 100ms) for continuous data monitoring
- **Real-time Data Charting**: Floating chart window with professional visualization capabilities:
  - Auto-scaling time and value axes with grid lines and tick marks
  - Color-coded data series with visual indicators
  - Support for 100,000+ data points with intelligent data decimation
  - Pause/resume and clear chart controls
  - Smart data type conversion for optimal visualization
- Configurable start address and quantity for read operations
- Multiple data type display options for register values:
  - UInt16, Int16 (single register values)
  - UInt32, Int32, Float32 (double register values)
  - Float64 (quad register values)
  - ASCII with configurable byte order (MSB/LSB first)
  - Hex and Binary display with enhanced formatting
- Reverse register order option for compatibility with non-standard Modbus implementations
- Customisable request parameters

## Slave Mode Features

- Configurable register tables:
  - Holding Registers (read/write)
  - Input Registers (read-only)
  - Coils (read/write boolean)
  - Discrete Inputs (read-only boolean)
- Register management with address, name, description, and data type properties
- Value modification with real-time updates
- Import/Export register configurations
- Individual data type selection for each register
- Configurable slave ID for multi-device simulation
- Improved register addition workflow with better error handling
- **TCP Connection Monitoring**: Real-time detection of master connections and disconnections
- **Visual Connection Status**: Smart LED indicators showing connection state:
  - Gray: Disconnected
  - Green: Slave listening, no masters connected
  - Blue: Master connected to slave
  - Red: Connection failed
- **Master Connection Logging**: Automatic logging with IP address and hostname resolution

## Listen In Mode Features

- **Passive Monitoring**: Monitor Modbus RTU communications without interfering with existing master/slave operations
- **Comprehensive Data Capture**: Capture all Modbus messages with detailed information:
  - Timestamp with millisecond precision
  - Communication direction (Master → Slave, Slave → Master)
  - Slave ID and function code
  - Data length and CRC validation status
  - Parsed data interpretation
  - Raw message bytes
- **Real-time Display**: Live view of captured messages in an organized table format
- **Data Export Options**: 
  - Export to CSV format for analysis in Excel and other spreadsheet tools
  - Export to text format for legacy compatibility
- **Message Management**: Clear captured messages and copy to clipboard functionality
- **COM Port Compatibility**: Works with any RTU connection parameters (baud rate, parity, data bits, stop bits)

## Profile Management

- Save and load connection profiles
- Default profile auto-loading on startup
- Quick switching between saved configurations
- Protection for the Default Profile

## Usage

1. Select your connection type (TCP or RTU)
2. Configure appropriate connection parameters
3. Select operation mode (Master, Slave, or Listen In)
4. Connect to your Modbus device
5. **Master Mode**: Send requests, enable continuous polling, or open the real-time chart window
6. **Slave Mode**: Configure registers and respond to incoming requests
7. **Listen In Mode**: Monitor existing Modbus communications and export captured data
8. View response data in your preferred format
9. Save your configuration as a named profile for future use

## Tip

Want to test it out but don't have any modbus devices to test with? Run two instances of Modbus Toolbox in TCP mode, one as master and one as slave. You can play around with different register structures and data types, send and receieve data, and see what the raw data bytes look like in the event log.

## Technical Details

- Built with .NET 9 and C#
- Uses NModbus library for Modbus protocol implementation
- MVVM architecture pattern
- WPF-based user interface with custom high-performance Canvas rendering
- Custom charting solution replacing LiveCharts for optimal performance
- Intelligent data decimation and virtualization for handling large datasets
- Serialisable profiles for configuration persistence

## License

Copyright (c) 2025 Peake Electronic Innovation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
OR OTHER DEALINGS IN THE SOFTWARE.