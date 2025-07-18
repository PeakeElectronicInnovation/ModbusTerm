using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ModbusTerm.Models;
using ModbusTerm.ViewModels;

namespace ModbusTerm;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// The main view model for the application
    /// </summary>
    private MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize the view model
        _viewModel = new MainViewModel();
        
        // Set the data context
        DataContext = _viewModel;
        
        // Show the appropriate UI based on initial mode
        UpdateUIForMode(_viewModel.IsMasterMode);
        
        // Subscribe to collection changed events for auto-scrolling
        _viewModel.CommunicationEvents.CollectionChanged += CommunicationEvents_CollectionChanged;
        
        // Subscribe to property changed events for connection parameters
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }
    
    // Flag to prevent recursive event triggering
    private bool _isUpdatingUI = false;
    
    /// <summary>
    /// Event handler for the connection type radio buttons
    /// </summary>
    /// <param name="sender">The radio button that triggered the event</param>
    /// <param name="e">Event arguments</param>
    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        // Prevent recursive calls
        if (_isUpdatingUI)
            return;
            
        if (TcpParametersPanel == null || RtuParametersPanel == null)
            return;
            
        if (sender is RadioButton radioButton)
        {
            try
            {
                _isUpdatingUI = true;
                
                // Get the connection type from the radio button tag
                var connectionType = radioButton.Tag?.ToString() ?? string.Empty;
                
                // Show the appropriate parameter panel
                if (connectionType == "TCP")
                {
                    TcpParametersPanel.Visibility = Visibility.Visible;
                    RtuParametersPanel.Visibility = Visibility.Collapsed;
                    
                    // Update the view model
                    if (_viewModel != null)
                        _viewModel.ChangeConnectionType(ConnectionType.TCP);
                }
                else if (connectionType == "RTU")
                {
                    TcpParametersPanel.Visibility = Visibility.Collapsed;
                    RtuParametersPanel.Visibility = Visibility.Visible;
                    
                    // Update the view model
                    if (_viewModel != null)
                        _viewModel.ChangeConnectionType(ConnectionType.RTU);
                }
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }
    }
    
    /// <summary>
    /// Event handler for the mode selection radio buttons
    /// </summary>
    /// <param name="sender">The radio button that triggered the event</param>
    /// <param name="e">Event arguments</param>
    private void ModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;
            
        if (sender is RadioButton radioButton)
        {
            // Get the mode from the radio button tag
            var mode = radioButton.Tag?.ToString() ?? string.Empty;
            
            // Update UI based on the mode
            UpdateUIForMode(mode == "Master");
        }
    }
    
    /// <summary>
    /// Updates the UI based on the selected mode (master/slave)
    /// </summary>
    /// <param name="isMaster">True if master mode, false if slave mode</param>
    private void UpdateUIForMode(bool isMaster)
    {
        // The TabControl's tabs are bound to IsMasterMode/IsSlaveMode properties
        // and use the BooleanToVisibilityConverter to show/hide appropriately
        
        // Additional UI updates for mode switching can be added here if needed
        if (_viewModel != null)
        {
            if (isMaster)
            {
                _viewModel.CreateReadRequest(ModbusFunctionCode.ReadHoldingRegisters);
                
                // Make sure the function code combo box reflects the current function
                if (FunctionCodeComboBox != null)
                {
                    FunctionCodeComboBox.SelectedIndex = 2; // Default to Read Holding Registers
                }
            }
        }
    }
    
    /// <summary>
    /// Event handler for the function code selection changed event
    /// </summary>
    /// <param name="sender">The combo box that triggered the event</param>
    /// <param name="e">Event arguments</param>
    private void FunctionCodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || FunctionCodeComboBox == null || FunctionCodeComboBox.SelectedItem == null)
            return;
            
        if (FunctionCodeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
        {
            // Get the function code from the selected item's tag
            if (int.TryParse(selectedItem.Tag.ToString(), out int functionCode))
            {
                // Create the appropriate request based on the function code
                switch (functionCode)
                {
                    case 1: // Read Coils
                        _viewModel.CreateReadRequest(ModbusFunctionCode.ReadCoils);
                        break;
                        
                    case 2: // Read Discrete Inputs
                        _viewModel.CreateReadRequest(ModbusFunctionCode.ReadDiscreteInputs);
                        break;
                        
                    case 3: // Read Holding Registers
                        _viewModel.CreateReadRequest(ModbusFunctionCode.ReadHoldingRegisters);
                        break;
                        
                    case 4: // Read Input Registers
                        _viewModel.CreateReadRequest(ModbusFunctionCode.ReadInputRegisters);
                        break;
                        
                    case 5: // Write Single Coil
                        _viewModel.CreateWriteSingleCoilRequest();
                        break;
                        
                    case 6: // Write Single Register
                        _viewModel.CreateWriteSingleRegisterRequest();
                        break;
                        
                    case 15: // Write Multiple Coils
                        _viewModel.CreateWriteMultipleCoilsRequest();
                        break;
                        
                    case 16: // Write Multiple Registers
                        _viewModel.CreateWriteMultipleRegistersRequest();
                        break;
                }
            }
        }
    }
    
    /// <summary>
    /// Event handler for loading rows in the event log data grid
    /// </summary>
    private void EventLogDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        // Add row number to the row header
        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    }
    
    /// <summary>
    /// Event handler for when the communication events collection changes
    /// Implements auto-scrolling behavior for the event log
    /// </summary>
    private void CommunicationEvents_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Only auto-scroll if the AutoScrollEventLog setting is enabled
        if (_viewModel.AutoScrollEventLog && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            // Use lower priority to ensure UI updates are complete before scrolling
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    // Wait for any pending layout or render operations
                    EventLogDataGrid.UpdateLayout();
                    
                    // Scroll to the bottom of the DataGrid using the ScrollViewer
                    var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(EventLogDataGrid);
                    if (scrollViewer != null)
                    {
                        scrollViewer.ScrollToEnd();
                    }
                }
                catch (Exception ex)
                {
                    // Log exception but don't crash
                    System.Diagnostics.Debug.WriteLine($"Auto-scroll error: {ex.Message}");
                }
            }));
        }
    }
    
    /// <summary>
    /// Helper method to find a child control of a specific type
    /// </summary>
    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            
            if (child is T result)
            {
                return result;
            }
            else
            {
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Event handler for the COM port refresh button click
    /// </summary>
    /// <param name="sender">The button that was clicked</param>
    /// <param name="e">Event arguments</param>
    private void RefreshComPorts_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RefreshComPorts();
        }
    }
    
    /// <summary>
    /// Event handler for property changes in the view model
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Prevent recursive calls
        if (_isUpdatingUI)
            return;
            
        // Handle connection parameters change to update UI accordingly
        if (e.PropertyName == nameof(MainViewModel.ConnectionParameters))
        {
            try 
            {
                _isUpdatingUI = true;
                
                // Update connection type radio buttons based on loaded connection parameters
                if (_viewModel.ConnectionParameters != null && ConnectionTypePanel != null)
                {
                    // Find the connection type radio buttons by iterating through the children
                    foreach (UIElement element in ConnectionTypePanel.Children)
                    {
                        if (element is RadioButton radioButton && radioButton.Tag != null)
                        {
                            string buttonType = radioButton.Tag?.ToString() ?? string.Empty;
                            
                            // Check if this radio button matches the connection parameters type
                            if (_viewModel.ConnectionParameters is TcpConnectionParameters && buttonType == "TCP")
                            {
                                radioButton.IsChecked = true;
                                // Show/hide panels directly to avoid circular references
                                if (TcpParametersPanel != null && RtuParametersPanel != null)
                                {
                                    TcpParametersPanel.Visibility = Visibility.Visible;
                                    RtuParametersPanel.Visibility = Visibility.Collapsed;
                                }
                                break;
                            }
                            else if (_viewModel.ConnectionParameters is RtuConnectionParameters && buttonType == "RTU")
                            {
                                radioButton.IsChecked = true;
                                // Show/hide panels directly to avoid circular references
                                if (TcpParametersPanel != null && RtuParametersPanel != null)
                                {
                                    TcpParametersPanel.Visibility = Visibility.Collapsed;
                                    RtuParametersPanel.Visibility = Visibility.Visible;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }
    }
    
    /// <summary>
    /// Event handler for the baud rate combo box selection changed event.
    /// Toggles the visibility of the custom baud rate input field.
    /// </summary>
    /// <param name="sender">The combo box that triggered the event</param>
    /// <param name="e">Event arguments</param>
    private void BaudRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Debug logging
        System.Diagnostics.Debug.WriteLine($"BaudRateComboBox_SelectionChanged event fired");
        
        if (_viewModel == null || !(sender is ComboBox))
            return;
            
        // Check if we have RTU connection parameters
        if (_viewModel.ConnectionParameters is RtuConnectionParameters rtuParams)
        {
            System.Diagnostics.Debug.WriteLine($"Current BaudRate: {rtuParams.BaudRate}");
            
            // If the selected baud rate is -1 (Custom), show the custom baud rate input
            bool isCustomSelected = rtuParams.BaudRate == -1;
            System.Diagnostics.Debug.WriteLine($"isCustomSelected: {isCustomSelected}");
            
            rtuParams.UseCustomBaudRate = isCustomSelected;
            System.Diagnostics.Debug.WriteLine($"UseCustomBaudRate set to: {rtuParams.UseCustomBaudRate}");
            
            // If switching away from custom, restore a standard baud rate if needed
            if (!isCustomSelected && rtuParams.BaudRate == -1)
            {
                rtuParams.BaudRate = 9600; // Default to 9600 as fallback
                System.Diagnostics.Debug.WriteLine($"Restored BaudRate to 9600");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("_viewModel.ConnectionParameters is not RtuConnectionParameters");
        }
    }
    
    /// <summary>
    /// Event handler for CheckBox Checked event - manually updates BooleanValue
    /// </summary>
    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is WriteDataItemViewModel item)
        {
            item.BooleanValue = true;
        }
    }
    
    /// <summary>
    /// Event handler for CheckBox Unchecked event - manually updates BooleanValue
    /// </summary>
    private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is WriteDataItemViewModel item)
        {
            item.BooleanValue = false;
        }
    }
}