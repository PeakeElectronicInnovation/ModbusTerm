using System;
using System.Collections.Generic;
using System.Linq;
using NModbus;
using NModbus.Data;
using NModbus.Device;

namespace ModbusTerm.Services
{
    /// <summary>
    /// An implementation of ISlaveDataStore that notifies when register values are changed externally
    /// </summary>
    public class NotifyingSlaveDataStore : ISlaveDataStore
    {
        private readonly DefaultSlaveDataStore _innerDataStore;
        
        /// <summary>
        /// Event raised when a holding register is changed by an external Modbus master
        /// </summary>
        public event EventHandler<RegisterChangedEventArgs>? HoldingRegisterChanged;
        
        /// <summary>
        /// Event raised when a coil is changed by an external Modbus master
        /// </summary>
        public event EventHandler<CoilChangedEventArgs>? CoilChanged;
        
        // Wrapping the holding registers with a notifying source
        private readonly NotifyingPointSource<ushort> _notifyingHoldingRegisters;
        
        // Wrapping the coils with a notifying source
        private readonly NotifyingPointSource<bool> _notifyingCoils;
        
        /// <summary>
        /// Gets the coils (read/write) data store with notification on changes
        /// </summary>
        public IPointSource<bool> CoilDiscretes => _notifyingCoils;
        
        /// <summary>
        /// Gets the coil inputs (read-only) data store
        /// </summary>
        public IPointSource<bool> CoilInputs => _innerDataStore.CoilInputs;
        
        /// <summary>
        /// Gets the input registers (read-only) data store
        /// </summary>
        public IPointSource<ushort> InputRegisters => _innerDataStore.InputRegisters;
        
        /// <summary>
        /// Gets the holding registers (read/write) data store with notification on changes
        /// </summary>
        public IPointSource<ushort> HoldingRegisters => _notifyingHoldingRegisters;
        
        /// <summary>
        /// Creates a new instance of NotifyingSlaveDataStore wrapping a default data store
        /// </summary>
        public NotifyingSlaveDataStore()
        {
            _innerDataStore = new DefaultSlaveDataStore();
            
            // Set up notifying holding registers
            _notifyingHoldingRegisters = new NotifyingPointSource<ushort>(_innerDataStore.HoldingRegisters);
            _notifyingHoldingRegisters.PointsWritten += NotifyingHoldingRegisters_PointsWritten;
            
            // Set up notifying coils
            _notifyingCoils = new NotifyingPointSource<bool>(_innerDataStore.CoilDiscretes);
            _notifyingCoils.PointsWritten += NotifyingCoils_PointsWritten;
        }
        
        private void NotifyingHoldingRegisters_PointsWritten(object? sender, PointsWrittenEventArgs<ushort> e)
        {
            // Notify listeners that holding registers were changed externally
            HoldingRegisterChanged?.Invoke(this, new RegisterChangedEventArgs(
                e.StartAddress,
                e.Values
            ));
        }
        
        private void NotifyingCoils_PointsWritten(object? sender, PointsWrittenEventArgs<bool> e)
        {
            // Notify listeners that coils were changed externally
            CoilChanged?.Invoke(this, new CoilChangedEventArgs(
                e.StartAddress,
                e.Values
            ));
        }
    }
    
    /// <summary>
    /// A wrapper for IPointSource that notifies when points are written externally
    /// </summary>
    internal class NotifyingPointSource<T> : IPointSource<T>
    {
        private readonly IPointSource<T> _innerSource;
        private bool _suppressNotifications = false;
        
        /// <summary>
        /// Event raised when points are written to the collection
        /// </summary>
        public event EventHandler<PointsWrittenEventArgs<T>>? PointsWritten;
        
        /// <summary>
        /// Flag to control whether notifications are raised on write
        /// </summary>
        public bool SuppressNotifications
        {
            get => _suppressNotifications;
            set => _suppressNotifications = value;
        }
        
        /// <summary>
        /// Creates a new instance wrapping an inner point source
        /// </summary>
        public NotifyingPointSource(IPointSource<T> innerSource)
        {
            _innerSource = innerSource;
        }
        
        /// <summary>
        /// Reads points from the underlying collection
        /// </summary>
        public T[] ReadPoints(ushort startAddress, ushort numberOfPoints)
        {
            return _innerSource.ReadPoints(startAddress, numberOfPoints);
        }
        
        /// <summary>
        /// Writes points to the underlying collection and raises the PointsWritten event
        /// </summary>
        public void WritePoints(ushort startAddress, T[] values)
        {
            // Write to the underlying source first
            _innerSource.WritePoints(startAddress, values);
            
            // Only raise events if notifications aren't suppressed
            // This lets us distinguish between internal writes (from our app) 
            // and external writes (from a Modbus master device)
            if (!_suppressNotifications)
            {
                PointsWritten?.Invoke(this, new PointsWrittenEventArgs<T>(startAddress, values));
            }
        }
    }
    
    /// <summary>
    /// Event arguments for when register points are written
    /// </summary>
    public class PointsWrittenEventArgs<T> : EventArgs
    {
        /// <summary>
        /// The start address that was written to
        /// </summary>
        public ushort StartAddress { get; }
        
        /// <summary>
        /// The values that were written
        /// </summary>
        public T[] Values { get; }
        
        /// <summary>
        /// Creates a new event arguments instance
        /// </summary>
        public PointsWrittenEventArgs(ushort startAddress, T[] values)
        {
            StartAddress = startAddress;
            Values = values;
        }
    }
    
    /// <summary>
    /// Event arguments for when registers are changed
    /// </summary>
    public class RegisterChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The start address of the registers that changed
        /// </summary>
        public ushort StartAddress { get; }
        
        /// <summary>
        /// The new values for the registers
        /// </summary>
        public ushort[] Values { get; }
        
        /// <summary>
        /// Creates a new event arguments instance
        /// </summary>
        public RegisterChangedEventArgs(ushort startAddress, ushort[] values)
        {
            StartAddress = startAddress;
            Values = values;
        }
    }
    
    /// <summary>
    /// Event arguments for when coils are changed
    /// </summary>
    public class CoilChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The start address of the coils that changed
        /// </summary>
        public ushort StartAddress { get; }
        
        /// <summary>
        /// The new values for the coils
        /// </summary>
        public bool[] Values { get; }
        
        /// <summary>
        /// Creates a new event arguments instance
        /// </summary>
        public CoilChangedEventArgs(ushort startAddress, bool[] values)
        {
            StartAddress = startAddress;
            Values = values;
        }
    }
}
