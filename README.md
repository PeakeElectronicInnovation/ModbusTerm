# ModbusTermAdd commentMore actions

A comprehensive Modbus testing application supporting both TCP and RTU connections with master and slave device functionality.

## Features

- **Dual Connection Types**: Support for both Modbus TCP (network-based) and Modbus RTU (serial-based) connections
- **Master/Slave Modes**: Function as either a Modbus master (client) or slave (server) device
- **Connection Profiles**: Save and load connection settings with profile management
- **Data Visualization**: View and interact with Modbus registers in various data formats (UInt16, Int16, Float, etc.)
- **Real-time Monitoring**: Track communication events through an integrated event log
- **Register Management**: Define and manage custom register configurations

## Connection Options

### TCP Mode
- IP Address/Hostname configuration
- Custom port settings (default: 502)
- TCP Master and TCP Slave functionality

### RTU Mode
- COM port selection and configuration
- Adjustable baud rate, parity, data bits, and stop bits
- Support for custom baud rates
- RTU Master and RTU Slave functionality
Add commentMore actions
## Usage

1. Select your connection type (TCP or RTU)
2. Configure appropriate connection parameters
3. Select master or slave mode
4. Connect to your Modbus device
5. Send requests (master mode) or respond to incoming requests (slave mode)
6. Save your configuration as a named profile for future use

## Technical Details

- Built with .NET and C#
- Uses NModbus library for Modbus protocol implementation
- MVVM architecture pattern
- WPF-based user interface