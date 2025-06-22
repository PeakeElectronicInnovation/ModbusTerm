using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModbusTerm.Models
{
    /// <summary>
    /// Defines a boolean register (coil or discrete input) in the slave mode
    /// </summary>
    public class BooleanRegisterDefinition : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private ushort _address;
        private bool _value;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private bool _suppressNotifications = false;
        private bool _isRecentlyModified = false;
        
        /// <summary>
        /// Gets or sets the register address
        /// </summary>
        public ushort Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the boolean value
        /// </summary>
        public bool Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    
                    // Always notify for Value changes regardless of suppression setting
                    // This is critical for external writes to show immediately in UI
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    
                    // Standard notification for other bound properties
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(FormattedValue));
                }
            }
        }

        /// <summary>
        /// Gets or sets the register name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the register description
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    NotifyPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether property change notifications are suppressed
        /// </summary>
        public bool SuppressNotifications
        {
            get => _suppressNotifications;
            set => _suppressNotifications = value;
        }

        /// <summary>
        /// Gets or sets whether this register was recently modified by an external master
        /// </summary>
        public bool IsRecentlyModified
        {
            get => _isRecentlyModified;
            set
            {
                if (_isRecentlyModified != value)
                {
                    _isRecentlyModified = value;
                    NotifyPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets the formatted representation of the value
        /// </summary>
        public string FormattedValue => Value ? "TRUE" : "FALSE";
        
        /// <summary>
        /// Force a property changed notification for a specific property
        /// </summary>
        /// <param name="propertyName">The name of the property that changed</param>
        public void ForcePropertyChanged(string propertyName)
        {
            NotifyPropertyChanged(propertyName);
        }
        
        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            // Only notify if notifications aren't suppressed
            if (!_suppressNotifications)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
