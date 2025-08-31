using ModbusTerm.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ModbusTerm.Views
{
    /// <summary>
    /// Interaction logic for ChartWindow.xaml
    /// </summary>
    public partial class ChartWindow : Window, INotifyPropertyChanged
    {
        private readonly Dictionary<ushort, List<ChartDataPoint>> _addressData = new Dictionary<ushort, List<ChartDataPoint>>();
        private readonly Dictionary<ushort, Polyline> _addressLines = new Dictionary<ushort, Polyline>();
        private readonly Dictionary<ushort, System.Windows.Controls.TextBox> _seriesNameTextBoxes = new Dictionary<ushort, System.Windows.Controls.TextBox>();
        private readonly Dictionary<ushort, System.Windows.Media.Color> _seriesColors = new Dictionary<ushort, System.Windows.Media.Color>();
        private readonly Dictionary<ushort, System.Windows.Controls.CheckBox> _seriesVisibilityCheckBoxes = new Dictionary<ushort, System.Windows.Controls.CheckBox>();
        private readonly Dictionary<ushort, System.Windows.Controls.Button> _seriesColorButtons = new Dictionary<ushort, System.Windows.Controls.Button>();
        private readonly Dictionary<ushort, System.Windows.Controls.TextBlock> _seriesValueLabels = new Dictionary<ushort, System.Windows.Controls.TextBlock>();
        private Dictionary<ushort, (System.Windows.Media.Color color, string name, bool visible)> _savedSeriesSettings = new Dictionary<ushort, (System.Windows.Media.Color, string, bool)>();
        private readonly System.Windows.Media.Color[] _colorPalette = {
            System.Windows.Media.Colors.Blue,
            System.Windows.Media.Colors.Red,
            System.Windows.Media.Colors.Green,
            System.Windows.Media.Colors.Orange,
            System.Windows.Media.Colors.Purple,
            System.Windows.Media.Colors.DarkCyan,
            System.Windows.Media.Colors.Magenta,
            System.Windows.Media.Colors.DarkGoldenrod,
            System.Windows.Media.Colors.DarkSlateBlue,
            System.Windows.Media.Colors.Crimson,
            System.Windows.Media.Colors.ForestGreen,
            System.Windows.Media.Colors.DarkOrange,
            System.Windows.Media.Colors.Indigo,
            System.Windows.Media.Colors.Teal,
            System.Windows.Media.Colors.Maroon,
            System.Windows.Media.Colors.Navy
        };
        private int _colorIndex = 0;
        
        private bool _isPaused = false;
        private int _maxPoints = 100000; // High-performance implementation can handle much more
        private ModbusDataType _currentDataType = ModbusDataType.UInt16;
        private DateTime _startTime = DateTime.Now;
        private bool _namesPanelVisible = true;
        private ushort _startAddress = 0;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private const int UPDATE_THROTTLE_MS = 16; // 60 FPS
        
        // Chart bounds and scaling
        private double _minX = 0, _maxX = 10;
        private double _minY = 0, _maxY = 100;
        private double _chartWidth = 800;
        private double _chartHeight = 400;
        private bool _autoScale = true;
        private bool _needsRedraw = false;
        private double _timeScaleMinutes = 10; // Default 10 minute window
        private bool _useRealTimestamps = true;
        
        // Mouse interaction
        private bool _isDragging = false;
        private System.Windows.Point _lastMousePosition;
        private Line? _crosshairX = null;
        private Line? _crosshairY = null;
        private TextBlock? _tooltipText = null;
        
        // Window docking
        private Window? _parentWindow = null;
        private bool _isDocked = false;
        
        // Tooltip persistence
        private System.Windows.Threading.DispatcherTimer? _tooltipTimer = null;
        private const int TOOLTIP_DELAY_MS = 8000; // 8 seconds

        public event PropertyChangedEventHandler? PropertyChanged;

        public ChartWindow(Window? parentWindow = null)
        {
            InitializeComponent();
            DataContext = this;
            _parentWindow = parentWindow;
            
            // Initialize controls with current values
            if (MaxPointsTextBox != null)
            {
                MaxPointsTextBox.Text = _maxPoints.ToString();
            }
            if (TimeScaleTextBox != null)
            {
                TimeScaleTextBox.Text = _timeScaleMinutes.ToString();
            }
            
            // Set up window docking if parent provided
            if (_parentWindow != null)
            {
                SetupWindowDocking();
            }
            if (AutoScaleCheckBox != null)
            {
                AutoScaleCheckBox.IsChecked = _autoScale;
            }
            if (RealTimeCheckBox != null)
            {
                RealTimeCheckBox.IsChecked = _useRealTimestamps;
            }
            
            // Setup canvas size change handler
            if (ChartCanvas != null)
            {
                ChartCanvas.SizeChanged += ChartCanvas_SizeChanged;
                ChartCanvas.MouseMove += ChartCanvas_MouseMove;
                ChartCanvas.MouseLeave += ChartCanvas_MouseLeave;
                _chartWidth = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth : 800;
                _chartHeight = ChartCanvas.ActualHeight > 0 ? ChartCanvas.ActualHeight : 400;
            }
            
            // Start render timer for smooth updates
            var renderTimer = new System.Windows.Threading.DispatcherTimer();
            renderTimer.Interval = TimeSpan.FromMilliseconds(UPDATE_THROTTLE_MS);
            renderTimer.Tick += RenderTimer_Tick;
            renderTimer.Start();
            
            // Initialize tooltip timer
            _tooltipTimer = new System.Windows.Threading.DispatcherTimer();
            _tooltipTimer.Interval = TimeSpan.FromMilliseconds(TOOLTIP_DELAY_MS);
            _tooltipTimer.Tick += TooltipTimer_Tick;
        }
        
        /// <summary>
        /// Set up window docking functionality
        /// </summary>
        private void SetupWindowDocking()
        {
            if (_parentWindow == null) return;
            
            // Position chart window to the right of main window
            PositionRelativeToParent();
            
            // Subscribe to parent window events
            _parentWindow.LocationChanged += ParentWindow_LocationChanged;
            _parentWindow.SizeChanged += ParentWindow_SizeChanged;
            _parentWindow.StateChanged += ParentWindow_StateChanged;
            _parentWindow.Closed += ParentWindow_Closed;
            
            // Handle this window's events
            this.LocationChanged += ChartWindow_LocationChanged;
            this.Loaded += ChartWindow_Loaded;
        }
        
        /// <summary>
        /// Position chart window relative to parent window
        /// </summary>
        private void PositionRelativeToParent()
        {
            if (_parentWindow == null) return;
            
            // Position to the right of the main window
            this.Left = _parentWindow.Left + _parentWindow.Width + 10;
            this.Top = _parentWindow.Top;
            this.Height = _parentWindow.Height;
            this.Width = _parentWindow.Width; // Same width as parent
        }
        
        /// <summary>
        /// Handle parent window location changes
        /// </summary>
        private void ParentWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (_isDocked && _parentWindow != null)
            {
                PositionRelativeToParent();
            }
        }
        
        /// <summary>
        /// Handle parent window size changes
        /// </summary>
        private void ParentWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isDocked && _parentWindow != null)
            {
                PositionRelativeToParent();
            }
        }
        
        /// <summary>
        /// Handle parent window state changes
        /// </summary>
        private void ParentWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_parentWindow == null) return;
            
            if (_parentWindow.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Minimized;
            }
            else if (_parentWindow.WindowState == WindowState.Normal || _parentWindow.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                if (_isDocked)
                {
                    PositionRelativeToParent();
                }
            }
        }
        
        /// <summary>
        /// Handle parent window closing
        /// </summary>
        private void ParentWindow_Closed(object? sender, EventArgs e)
        {
            this.Close();
        }
        
        /// <summary>
        /// Handle chart window location changes to detect manual positioning
        /// </summary>
        private void ChartWindow_LocationChanged(object? sender, EventArgs e)
        {
            // If user manually moves the window, disable docking
            if (this.IsLoaded && _isDocked)
            {
                var expectedLeft = _parentWindow?.Left + _parentWindow?.Width + 10;
                var expectedTop = _parentWindow?.Top;
                
                if (Math.Abs(this.Left - (expectedLeft ?? 0)) > 5 || Math.Abs(this.Top - (expectedTop ?? 0)) > 5)
                {
                    _isDocked = false;
                }
            }
        }
        
        /// <summary>
        /// Handle chart window loaded event
        /// </summary>
        private void ChartWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            _isDocked = true;
            PositionRelativeToParent();
        }

        private void TimeScale_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes))
            {
                // Clamp between 0.1 min (6s) and 1440 min (1 day)
                minutes = Math.Max(0.1, Math.Min(1440, minutes));
                _timeScaleMinutes = minutes;

                // If autoscale is on, we recompute bounds on next redraw
                _needsRedraw = true;
            }
        }

        private void AutoScale_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (AutoScaleCheckBox?.IsChecked is bool isChecked)
            {
                _autoScale = isChecked;

                if (_autoScale)
                {
                    // Reset to trigger fresh autoscale on next redraw
                    _minX = 0; _maxX = Math.Max(_maxX, 10);
                    _minY = 0; _maxY = 1;
                }

                _needsRedraw = true;
            }
        }

        private void RealTime_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (RealTimeCheckBox?.IsChecked is bool isChecked)
            {
                _useRealTimestamps = isChecked;
                _needsRedraw = true; // Redraw to update X-axis label formatting
            }
        }
        
        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _chartWidth = e.NewSize.Width;
            _chartHeight = e.NewSize.Height;
            _needsRedraw = true;
        }
        
        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            if (_needsRedraw)
            {
                RedrawChart();
                _needsRedraw = false;
            }
        }

        /// <summary>
        /// Add data points to the chart
        /// </summary>
        public void AddDataPoints(ModbusResponseInfo response, ModbusDataType dataType, bool reverseRegisterOrder, ushort startAddress = 0)
        {
            if (_isPaused || response?.Data == null)
                return;

            try
            {
                _currentDataType = dataType;
                _startAddress = startAddress;
                
                // Safely update UI elements
                if (DataTypeText != null)
                {
                    DataTypeText.Text = GetDataTypeDisplayName(dataType);
                }
                
                var currentTime = DateTime.Now;
                var timeOffset = (currentTime - _startTime).TotalSeconds;
                
                if (response.Data is ushort[] registers)
                {
                    var chartableValues = ConvertToChartableValues(registers, dataType, reverseRegisterOrder);
                    var registerIncrement = GetRegisterIncrement(dataType);
                    
                    for (int i = 0; i < chartableValues.Count; i++)
                    {
                        var address = (ushort)(startAddress + (i * registerIncrement));
                        var value = chartableValues[i];
                        
                        AddDataPoint(address, timeOffset, value, currentTime);
                    }
                }
                else if (response.Data is bool[] bools)
                {
                    for (int i = 0; i < bools.Length; i++)
                    {
                        var address = (ushort)(startAddress + i);
                        var value = bools[i] ? 1.0 : 0.0;
                        
                        AddDataPoint(address, timeOffset, value, currentTime);
                    }
                }
                
                UpdatePointCount();
                _needsRedraw = true;
            }
            catch (Exception ex)
            {
                if (StatusText != null)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                }
            }
        }
        
        /// <summary>
        /// Add a single data point efficiently
        /// </summary>
        private void AddDataPoint(ushort address, double timeOffset, double value, DateTime timestamp)
        {
            if (!_addressData.ContainsKey(address))
            {
                CreateSeriesForAddress(address);
            }
            
            var dataPoint = new ChartDataPoint
            {
                TimeOffset = timeOffset,
                Value = value,
                Timestamp = timestamp
            };
            
            var dataList = _addressData[address];
            dataList.Add(dataPoint);
            
            // Efficient removal when limit exceeded
            if (dataList.Count > _maxPoints)
            {
                var removeCount = Math.Min(1000, dataList.Count - _maxPoints + 1000);
                dataList.RemoveRange(0, removeCount);
            }
            
            // Update latest value label
            if (_seriesValueLabels.ContainsKey(address))
            {
                _seriesValueLabels[address].Text = FormatValue(value);
            }
            
            // Update bounds for auto-scaling
            if (_autoScale)
            {
                if (timeOffset < _minX) _minX = timeOffset;
                if (timeOffset > _maxX) _maxX = timeOffset;
                if (value < _minY) _minY = value;
                if (value > _maxY) _maxY = value;
            }
            
            _needsRedraw = true;
        }

        /// <summary>
        /// Format value for display in latest value column
        /// </summary>
        private string FormatValue(double value)
        {
            if (Math.Abs(value) < 0.01)
                return "0";
            else if (Math.Abs(value) >= 1000)
                return value.ToString("F0");
            else if (Math.Abs(value) >= 1)
                return value.ToString("F2");
            else
                return value.ToString("F3");
        }

        /// <summary>
        /// Convert register values to chartable values based on data type
        /// </summary>
        private List<double> ConvertToChartableValues(ushort[] registers, ModbusDataType dataType, bool reverseRegisterOrder)
        {
            var values = new List<double>();
            
            switch (dataType)
            {
                case ModbusDataType.UInt16:
                    values.AddRange(registers.Select(r => (double)r));
                    break;
                    
                case ModbusDataType.Int16:
                    values.AddRange(registers.Select(r => (double)(short)r));
                    break;
                    
                case ModbusDataType.UInt32:
                    for (int i = 0; i < registers.Length - 1; i += 2)
                    {
                        uint value;
                        if (reverseRegisterOrder)
                        {
                            value = (uint)((registers[i] << 16) | registers[i + 1]);
                        }
                        else
                        {
                            value = (uint)((registers[i + 1] << 16) | registers[i]);
                        }
                        values.Add(value);
                    }
                    break;
                    
                case ModbusDataType.Int32:
                    for (int i = 0; i < registers.Length - 1; i += 2)
                    {
                        int value;
                        if (reverseRegisterOrder)
                        {
                            value = (int)((registers[i] << 16) | registers[i + 1]);
                        }
                        else
                        {
                            value = (int)((registers[i + 1] << 16) | registers[i]);
                        }
                        values.Add(value);
                    }
                    break;
                    
                case ModbusDataType.Float32:
                    for (int i = 0; i < registers.Length - 1; i += 2)
                    {
                        byte[] bytes = new byte[4];
                        
                        if (reverseRegisterOrder)
                        {
                            bytes[0] = (byte)(registers[i + 1] & 0xFF);
                            bytes[1] = (byte)(registers[i + 1] >> 8);
                            bytes[2] = (byte)(registers[i] & 0xFF);
                            bytes[3] = (byte)(registers[i] >> 8);
                        }
                        else
                        {
                            bytes[0] = (byte)(registers[i] & 0xFF);
                            bytes[1] = (byte)(registers[i] >> 8);
                            bytes[2] = (byte)(registers[i + 1] & 0xFF);
                            bytes[3] = (byte)(registers[i + 1] >> 8);
                        }
                        
                        float value = BitConverter.ToSingle(bytes, 0);
                        values.Add(value);
                    }
                    break;
                    
                case ModbusDataType.Float64:
                    for (int i = 0; i < registers.Length - 3; i += 4)
                    {
                        byte[] bytes = new byte[8];
                        
                        if (reverseRegisterOrder)
                        {
                            bytes[0] = (byte)(registers[i + 3] & 0xFF);
                            bytes[1] = (byte)(registers[i + 3] >> 8);
                            bytes[2] = (byte)(registers[i + 2] & 0xFF);
                            bytes[3] = (byte)(registers[i + 2] >> 8);
                            bytes[4] = (byte)(registers[i + 1] & 0xFF);
                            bytes[5] = (byte)(registers[i + 1] >> 8);
                            bytes[6] = (byte)(registers[i] & 0xFF);
                            bytes[7] = (byte)(registers[i] >> 8);
                        }
                        else
                        {
                            bytes[0] = (byte)(registers[i] & 0xFF);
                            bytes[1] = (byte)(registers[i] >> 8);
                            bytes[2] = (byte)(registers[i + 1] & 0xFF);
                            bytes[3] = (byte)(registers[i + 1] >> 8);
                            bytes[4] = (byte)(registers[i + 2] & 0xFF);
                            bytes[5] = (byte)(registers[i + 2] >> 8);
                            bytes[6] = (byte)(registers[i + 3] & 0xFF);
                            bytes[7] = (byte)(registers[i + 3] >> 8);
                        }
                        
                        double value = BitConverter.ToDouble(bytes, 0);
                        values.Add(value);
                    }
                    break;
                    
                // Skip non-chartable types like ASCII strings
                case ModbusDataType.AsciiString:
                default:
                    break;
            }
            
            return values;
        }

        /// <summary>
        /// Get register increment based on data type
        /// </summary>
        private int GetRegisterIncrement(ModbusDataType dataType)
        {
            return dataType switch
            {
                ModbusDataType.UInt16 => 1,
                ModbusDataType.Int16 => 1,
                ModbusDataType.UInt32 => 2,
                ModbusDataType.Int32 => 2,
                ModbusDataType.Float32 => 2,
                ModbusDataType.Float64 => 4,
                _ => 1
            };
        }

        /// <summary>
        /// Create a new series for the given address
        /// </summary>
        private void CreateSeriesForAddress(ushort address)
        {
            // Initialize data list
            _addressData[address] = new List<ChartDataPoint>();
            
            // Restore saved settings or use defaults
            System.Windows.Media.Color color;
            string seriesName;
            bool isVisible;
            
            if (_savedSeriesSettings.ContainsKey(address))
            {
                var saved = _savedSeriesSettings[address];
                color = saved.color;
                seriesName = saved.name;
                isVisible = saved.visible;
            }
            else
            {
                color = _colorPalette[_colorIndex % _colorPalette.Length];
                seriesName = $"Register {address}";
                isVisible = true;
                _colorIndex++;
            }
            
            _seriesColors[address] = color;
            
            // Create polyline for rendering
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.Transparent
            };
            
            _addressLines[address] = polyline;
            
            if (ChartCanvas != null)
            {
                ChartCanvas.Children.Add(polyline);
            }
            
            // Create editable name control
            CreateSeriesNameControl(address, seriesName, isVisible);
        }

        /// <summary>
        /// Create editable name control for a series
        /// </summary>
        private void CreateSeriesNameControl(ushort address, string seriesName, bool isVisible)
        {
            if (SeriesNamesPanel == null) return;
            
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // Checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });  // Color button
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });  // Address
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Name (wider)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Latest value
            
            // Visibility checkbox
            var visibilityCheckBox = new System.Windows.Controls.CheckBox
            {
                IsChecked = isVisible,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Tag = address
            };
            Grid.SetColumn(visibilityCheckBox, 0);
            
            // Color button
            var colorButton = new System.Windows.Controls.Button
            {
                Width = 20,
                Height = 15,
                Background = new SolidColorBrush(_seriesColors[address]),
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Tag = address
            };
            colorButton.Click += ColorButton_Click;
            Grid.SetColumn(colorButton, 1);
            
            // Address label
            var addressLabel = new TextBlock
            {
                Text = address.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontSize = 10
            };
            Grid.SetColumn(addressLabel, 2);
            
            // Name textbox
            var nameTextBox = new System.Windows.Controls.TextBox
            {
                Text = seriesName,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2),
                FontSize = 10,
                Tag = address,
                Margin = new Thickness(2, 0, 2, 0)
            };
            Grid.SetColumn(nameTextBox, 3);
            
            // Latest value label (separate from name)
            var valueLabel = new System.Windows.Controls.TextBlock
            {
                Text = "-",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                FontSize = 10,
                Margin = new Thickness(5, 0, 2, 0),
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };
            Grid.SetColumn(valueLabel, 4);
            
            // Add all controls to grid
            grid.Children.Add(visibilityCheckBox);
            grid.Children.Add(colorButton);
            grid.Children.Add(addressLabel);
            grid.Children.Add(nameTextBox);
            grid.Children.Add(valueLabel);
            
            SeriesNamesPanel.Children.Add(grid);
            
            // Store references
            _seriesVisibilityCheckBoxes[address] = visibilityCheckBox;
            _seriesColorButtons[address] = colorButton;
            _seriesNameTextBoxes[address] = nameTextBox;
            _seriesValueLabels[address] = valueLabel;
            
            // Remove from saved settings since it's now created
            if (_savedSeriesSettings.ContainsKey(address))
            {
                _savedSeriesSettings.Remove(address);
            }
        }
        
        /// <summary>
        /// Clear chart data but preserve series settings (colors, names, visibility)
        /// </summary>
        public void ClearChart()
        {
            // Store current series settings
            var savedSettings = new Dictionary<ushort, (System.Windows.Media.Color color, string name, bool visible)>();
            foreach (var address in _addressData.Keys.ToList())
            {
                var color = _seriesColors.ContainsKey(address) ? _seriesColors[address] : _colorPalette[0];
                var name = _seriesNameTextBoxes.ContainsKey(address) ? _seriesNameTextBoxes[address].Text : $"Register {address}";
                var visible = _seriesVisibilityCheckBoxes.ContainsKey(address) ? _seriesVisibilityCheckBoxes[address].IsChecked == true : true;
                savedSettings[address] = (color, name, visible);
            }
            
            // Clear data and visual elements
            _addressData.Clear();
            _addressLines.Clear();
            
            if (ChartCanvas != null)
            {
                ChartCanvas.Children.Clear();
            }
            
            // Clear UI controls and their dictionaries to prevent duplication
            _seriesColors.Clear();
            _seriesNameTextBoxes.Clear();
            _seriesVisibilityCheckBoxes.Clear();
            _seriesColorButtons.Clear();
            _seriesValueLabels.Clear();
            _colorIndex = 0;
            
            if (SeriesNamesPanel != null)
            {
                SeriesNamesPanel.Children.Clear();
            }
            
            // Restore series settings when new data arrives
            _savedSeriesSettings = savedSettings;
            
            _needsRedraw = true;
            if (StatusText != null)
            {
                StatusText.Text = "Chart cleared";
            }
        }

        /// <summary>
        /// Update the point count display
        /// </summary>
        private void UpdatePointCount()
        {
            int totalPoints = _addressData.Values.Sum(v => v.Count);
            if (PointCountText != null)
            {
                PointCountText.Text = totalPoints.ToString();
            }
        }
        
        /// <summary>
        /// Redraw the entire chart efficiently
        /// </summary>
        private void RedrawChart()
        {
            if (ChartCanvas == null || _chartWidth <= 0 || _chartHeight <= 0)
                return;
                
            try
            {
                // Calculate margins for axes
                const double leftMargin = 60;
                const double bottomMargin = 40;
                const double topMargin = 20;
                const double rightMargin = 20;
                
                var plotWidth = _chartWidth - leftMargin - rightMargin;
                var plotHeight = _chartHeight - topMargin - bottomMargin;
                
                if (plotWidth <= 0 || plotHeight <= 0)
                    return;
                
                // Clear previous axis elements but preserve tooltip and crosshairs
                var elementsToRemove = ChartCanvas.Children.OfType<UIElement>()
                    .Where(e => (e is Line || e is TextBlock) && 
                               e != _crosshairX && e != _crosshairY && e != _tooltipText).ToList();
                foreach (var element in elementsToRemove)
                {
                    ChartCanvas.Children.Remove(element);
                }
                
                // Auto-scale if needed
                if (_autoScale && _addressData.Any())
                {
                    var visibleSeries = _addressData.Where(kvp => 
                        _seriesVisibilityCheckBoxes.ContainsKey(kvp.Key) && 
                        _seriesVisibilityCheckBoxes[kvp.Key].IsChecked == true);
                    
                    var allPoints = visibleSeries.SelectMany(kvp => kvp.Value).ToList();
                    if (allPoints.Any())
                    {
                        // Time scale management
                        var currentTime = (DateTime.Now - _startTime).TotalSeconds;
                        var timeWindowSeconds = _timeScaleMinutes * 60;
                        
                        _maxX = currentTime;
                        _minX = Math.Max(0, currentTime - timeWindowSeconds);
                        
                        // Remove whitespace by using actual data bounds
                        var dataMinX = allPoints.Min(p => p.TimeOffset);
                        var dataMaxX = allPoints.Max(p => p.TimeOffset);
                        
                        if (dataMaxX - dataMinX < timeWindowSeconds)
                        {
                            _minX = dataMinX;
                            _maxX = dataMaxX;
                        }
                        
                        _minY = allPoints.Min(p => p.Value);
                        _maxY = allPoints.Max(p => p.Value);
                        
                        // Add 5% padding for Y only
                        var yPadding = _maxY - _minY;
                        if (yPadding > 0)
                        {
                            _minY -= yPadding * 0.05;
                            _maxY += yPadding * 0.05;
                        }
                        else
                        {
                            _minY -= 1;
                            _maxY += 1;
                        }
                    }
                }
                
                var xRange = _maxX - _minX;
                var yRange = _maxY - _minY;
                
                if (xRange <= 0) xRange = 1;
                if (yRange <= 0) yRange = 1;
                
                // Draw grid lines and axes
                DrawAxesAndGrid(leftMargin, topMargin, plotWidth, plotHeight, xRange, yRange);
                
                // Update polylines for each series with data decimation for performance
                foreach (var kvp in _addressData)
                {
                    var address = kvp.Key;
                    var points = kvp.Value;
                    
                    if (_addressLines.ContainsKey(address) && points.Count > 0)
                    {
                        var polyline = _addressLines[address];
                        
                        // Check visibility
                        bool isVisible = _seriesVisibilityCheckBoxes.ContainsKey(address) && 
                                        _seriesVisibilityCheckBoxes[address].IsChecked == true;
                        polyline.Visibility = isVisible ? Visibility.Visible : Visibility.Hidden;
                        
                        if (isVisible)
                        {
                            var pointCollection = new PointCollection();
                            
                            // Filter points within visible range
                            var visiblePoints = points.Where(p => p.TimeOffset >= _minX && p.TimeOffset <= _maxX).ToList();
                            
                            if (visiblePoints.Any())
                            {
                                // Data decimation: if too many points, sample them
                                var maxRenderPoints = Math.Min(visiblePoints.Count, (int)plotWidth * 2);
                                var step = Math.Max(1, visiblePoints.Count / maxRenderPoints);
                                
                                for (int i = 0; i < visiblePoints.Count; i += step)
                                {
                                    var point = visiblePoints[i];
                                    var x = leftMargin + ((point.TimeOffset - _minX) / xRange) * plotWidth;
                                    var y = topMargin + plotHeight - ((point.Value - _minY) / yRange) * plotHeight;
                                    pointCollection.Add(new System.Windows.Point(x, y));
                                }
                                
                                // Always include the last point
                                if (step > 1 && visiblePoints.Count > 1)
                                {
                                    var lastPoint = visiblePoints[visiblePoints.Count - 1];
                                    var x = leftMargin + ((lastPoint.TimeOffset - _minX) / xRange) * plotWidth;
                                    var y = topMargin + plotHeight - ((lastPoint.Value - _minY) / yRange) * plotHeight;
                                    pointCollection.Add(new System.Windows.Point(x, y));
                                }
                            }
                            
                            polyline.Points = pointCollection;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (StatusText != null)
                {
                    StatusText.Text = $"Render error: {ex.Message}";
                }
            }
        }
        
        /// <summary>
        /// Draw axes and grid lines
        /// </summary>
        private void DrawAxesAndGrid(double leftMargin, double topMargin, double plotWidth, double plotHeight, double xRange, double yRange)
        {
            // Draw axes
            var xAxis = new Line
            {
                X1 = leftMargin,
                Y1 = topMargin + plotHeight,
                X2 = leftMargin + plotWidth,
                Y2 = topMargin + plotHeight,
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(xAxis);
            
            var yAxis = new Line
            {
                X1 = leftMargin,
                Y1 = topMargin,
                X2 = leftMargin,
                Y2 = topMargin + plotHeight,
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(yAxis);
            
            // Draw grid lines and labels
            const int numXTicks = 8;
            const int numYTicks = 6;
            
            // X-axis ticks and labels
            for (int i = 0; i <= numXTicks; i++)
            {
                var x = leftMargin + (i * plotWidth / numXTicks);
                var timeValue = _minX + (i * xRange / numXTicks);
                
                // Grid line
                var gridLine = new Line
                {
                    X1 = x,
                    Y1 = topMargin,
                    X2 = x,
                    Y2 = topMargin + plotHeight,
                    Stroke = System.Windows.Media.Brushes.LightGray,
                    StrokeThickness = 0.5
                };
                ChartCanvas.Children.Add(gridLine);
                
                // Label
                var label = new TextBlock
                {
                    Text = _useRealTimestamps ? 
                        _startTime.AddSeconds(timeValue).ToString("HH:mm:ss") :
                        TimeSpan.FromSeconds(timeValue).ToString(@"mm\:ss"),
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.Black
                };
                Canvas.SetLeft(label, x - 15);
                Canvas.SetTop(label, topMargin + plotHeight + 5);
                ChartCanvas.Children.Add(label);
            }
            
            // Y-axis ticks and labels
            for (int i = 0; i <= numYTicks; i++)
            {
                var y = topMargin + plotHeight - (i * plotHeight / numYTicks);
                var value = _minY + (i * yRange / numYTicks);
                
                // Grid line
                var gridLine = new Line
                {
                    X1 = leftMargin,
                    Y1 = y,
                    X2 = leftMargin + plotWidth,
                    Y2 = y,
                    Stroke = System.Windows.Media.Brushes.LightGray,
                    StrokeThickness = 0.5
                };
                ChartCanvas.Children.Add(gridLine);
                
                // Label
                var label = new TextBlock
                {
                    Text = value.ToString("F1"),
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.Black
                };
                Canvas.SetLeft(label, leftMargin - 50);
                Canvas.SetTop(label, y - 7);
                ChartCanvas.Children.Add(label);
            }
        }

        /// <summary>
        /// Get display name for data type
        /// </summary>
        private string GetDataTypeDisplayName(ModbusDataType dataType)
        {
            return dataType switch
            {
                ModbusDataType.UInt16 => "UInt16",
                ModbusDataType.Int16 => "Int16",
                ModbusDataType.UInt32 => "UInt32",
                ModbusDataType.Int32 => "Int32",
                ModbusDataType.Float32 => "Float32",
                ModbusDataType.Float64 => "Float64",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Check if data type is chartable
        /// </summary>
        public static bool IsChartableDataType(ModbusDataType dataType)
        {
            return dataType != ModbusDataType.AsciiString;
        }

        private void ClearChart_Click(object sender, RoutedEventArgs e)
        {
            ClearChart();
            if (StatusText != null)
            {
                StatusText.Text = "Chart cleared";
            }
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            if (StatusText != null)
            {
                StatusText.Text = _isPaused ? "Paused" : "Running";
            }
            
            if (sender is System.Windows.Controls.Button button)
            {
                button.Content = _isPaused ? "Resume" : "Pause";
            }
        }

        private void ToggleNamesPanel_Click(object sender, RoutedEventArgs e)
        {
            _namesPanelVisible = !_namesPanelVisible;
            
            if (_namesPanelVisible)
            {
                NamesColumn.Width = new GridLength(280); // Consistent width
                NamesPanel.Visibility = Visibility.Visible;
            }
            else
            {
                NamesColumn.Width = new GridLength(0);
                NamesPanel.Visibility = Visibility.Collapsed;
            }
            
            if (sender is System.Windows.Controls.Button button)
            {
                button.Content = _namesPanelVisible ? "Hide Settings" : "Show Settings";
                button.MinWidth = 80; // Ensure consistent button width
            }
        }

        private void MaxPoints_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && int.TryParse(textBox.Text, out int value))
            {
                if (value >= 100 && value <= 1000000)
                {
                    _maxPoints = value;
                    
                    // Trim existing data if new limit is lower
                    foreach (var kvp in _addressData)
                    {
                        var values = kvp.Value;
                        if (values.Count > _maxPoints)
                        {
                            var removeCount = values.Count - _maxPoints;
                            values.RemoveRange(0, removeCount);
                        }
                    }
                    
                    _needsRedraw = true;
                    
                    UpdatePointCount();
                }
                else
                {
                    // Reset to valid value if out of range
                    textBox.Text = _maxPoints.ToString();
                }
            }
        }
        
        /// <summary>
        /// Handle mouse wheel for zooming
        /// </summary>
        private void ChartCanvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var mousePos = e.GetPosition(ChartCanvas);
            var zoomFactor = e.Delta > 0 ? 0.9 : 1.1;
            
            var xRange = _maxX - _minX;
            var yRange = _maxY - _minY;
            
            // Calculate mouse position in data coordinates
            var mouseDataX = _minX + (mousePos.X / _chartWidth) * xRange;
            var mouseDataY = _maxY - (mousePos.Y / _chartHeight) * yRange;
            
            // Zoom around mouse position
            var newXRange = xRange * zoomFactor;
            var newYRange = yRange * zoomFactor;
            
            _minX = mouseDataX - (mouseDataX - _minX) * zoomFactor;
            _maxX = _minX + newXRange;
            _minY = mouseDataY - (mouseDataY - _minY) * zoomFactor;
            _maxY = _minY + newYRange;
            
            _autoScale = false; // Disable auto-scale when manually zooming
            if (AutoScaleCheckBox != null)
                AutoScaleCheckBox.IsChecked = false;
            _needsRedraw = true;
            
            UpdatePointCount();
        }

        /// <summary>
        /// Toggle series visibility
        /// </summary>
        private void ToggleSeriesVisibility(ushort address, bool isVisible)
        {
            if (_addressLines.ContainsKey(address))
            {
                _addressLines[address].Visibility = isVisible ? Visibility.Visible : Visibility.Hidden;
                _needsRedraw = true;
            }
        }
        
        /// <summary>
        /// Handle color button click - opens standard ColorDialog
        /// </summary>
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is ushort address)
            {
                var currentColor = _seriesColors[address];
                var colorDialog = new System.Windows.Forms.ColorDialog();
                colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);
                colorDialog.FullOpen = true; // Allow custom colors
                
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = System.Windows.Media.Color.FromArgb(
                        colorDialog.Color.A, 
                        colorDialog.Color.R, 
                        colorDialog.Color.G, 
                        colorDialog.Color.B);
                        
                    _seriesColors[address] = newColor;
                    button.Background = new SolidColorBrush(newColor);
                    
                    if (_addressLines.ContainsKey(address))
                    {
                        _addressLines[address].Stroke = new SolidColorBrush(newColor);
                    }
                    
                    button.ToolTip = $"Series color: {newColor} (click to change)";
                    RedrawChart();
                }
            }
        }
        
        /// <summary>
        /// Handle mouse move for crosshairs and tooltips
        /// </summary>
        private void ChartCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (ChartCanvas == null) return;
            
            var mousePos = e.GetPosition(ChartCanvas);
            
            // Handle panning
            if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var deltaX = mousePos.X - _lastMousePosition.X;
                var deltaY = mousePos.Y - _lastMousePosition.Y;
                
                var xRange = _maxX - _minX;
                var yRange = _maxY - _minY;
                
                var deltaTimeX = -(deltaX / _chartWidth) * xRange;
                var deltaValueY = (deltaY / _chartHeight) * yRange;
                
                _minX += deltaTimeX;
                _maxX += deltaTimeX;
                _minY += deltaValueY;
                _maxY += deltaValueY;
                
                _autoScale = false; // Disable auto-scale when manually panning
                if (AutoScaleCheckBox != null)
                    AutoScaleCheckBox.IsChecked = false;
                _needsRedraw = true;
                
                _lastMousePosition = mousePos;
                return;
            }
            
            // Stop tooltip timer since mouse is moving, then restart it
            _tooltipTimer?.Stop();
            
            // Update crosshairs
            UpdateCrosshairs(mousePos);
            
            // Check for series hover and show tooltip
            UpdateTooltip(mousePos);
            
            // Restart timer to hide tooltip after delay when mouse stops moving
            _tooltipTimer?.Start();
        }
        
        /// <summary>
        /// Handle mouse leave event
        /// </summary>
        private void ChartCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Start timer to hide tooltip after delay
            _tooltipTimer?.Start();
        }
        
        /// <summary>
        /// Handle tooltip timer tick to hide tooltip after delay
        /// </summary>
        private void TooltipTimer_Tick(object? sender, EventArgs e)
        {
            _tooltipTimer?.Stop();
            HideTooltip();
        }
        
        /// <summary>
        /// Handle mouse down for panning
        /// </summary>
        private void ChartCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(ChartCanvas);
                ChartCanvas?.CaptureMouse();
            }
        }
        
        /// <summary>
        /// Handle mouse up to stop panning
        /// </summary>
        private void ChartCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ChartCanvas?.ReleaseMouseCapture();
            }
        }
        
        /// <summary>
        /// Update crosshairs at mouse position
        /// </summary>
        private void UpdateCrosshairs(System.Windows.Point mousePos)
        {
            if (ChartCanvas == null) return;
            
            // Remove existing crosshairs
            if (_crosshairX != null)
            {
                ChartCanvas.Children.Remove(_crosshairX);
            }
            if (_crosshairY != null)
            {
                ChartCanvas.Children.Remove(_crosshairY);
            }
            
            // Create new crosshairs
            _crosshairX = new Line
            {
                X1 = 0,
                Y1 = mousePos.Y,
                X2 = _chartWidth,
                Y2 = mousePos.Y,
                Stroke = System.Windows.Media.Brushes.Gray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                IsHitTestVisible = false
            };
            
            _crosshairY = new Line
            {
                X1 = mousePos.X,
                Y1 = 0,
                X2 = mousePos.X,
                Y2 = _chartHeight,
                Stroke = System.Windows.Media.Brushes.Gray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                IsHitTestVisible = false
            };
            
            ChartCanvas.Children.Add(_crosshairX);
            ChartCanvas.Children.Add(_crosshairY);
        }
        
        /// <summary>
        /// Hide crosshair lines
        /// </summary>
        private void HideCrosshairs()
        {
            if (ChartCanvas == null) return;
            
            if (_crosshairX != null)
            {
                ChartCanvas.Children.Remove(_crosshairX);
                _crosshairX = null;
            }
            if (_crosshairY != null)
            {
                ChartCanvas.Children.Remove(_crosshairY);
                _crosshairY = null;
            }
        }
        
        /// <summary>
        /// Update tooltip with simplified information - only show closest series within 5px
        /// </summary>
        private void UpdateTooltip(System.Windows.Point mousePos)
        {
            if (ChartCanvas == null) return;
            
            const double leftMargin = 60;
            const double topMargin = 20;
            const double rightMargin = 20;
            const double bottomMargin = 40;
            const double proximityThreshold = 5.0; // 5 pixels
            
            // Use actual canvas dimensions
            var plotWidth = ChartCanvas.ActualWidth - leftMargin - rightMargin;
            var plotHeight = ChartCanvas.ActualHeight - topMargin - bottomMargin;
            
            // Convert mouse position to data coordinates
            var xRange = _maxX - _minX;
            var yRange = _maxY - _minY;
            
            if (mousePos.X >= leftMargin && mousePos.X <= leftMargin + plotWidth &&
                mousePos.Y >= topMargin && mousePos.Y <= topMargin + plotHeight)
            {
                var dataX = _minX + ((mousePos.X - leftMargin) / plotWidth) * xRange;
                var dataY = _minY + ((plotHeight - (mousePos.Y - topMargin)) / plotHeight) * yRange;
                
                string tooltipContent = $"X: {(_useRealTimestamps ? _startTime.AddSeconds(dataX).ToString("HH:mm:ss") : TimeSpan.FromSeconds(dataX).ToString(@"mm\:ss"))}\nY: {dataY:F2}";
                
                // Find closest series line within 5px
                string? closestSeriesName = null;
                double closestDistance = double.MaxValue;
                
                foreach (var kvp in _addressData)
                {
                    var address = kvp.Key;
                    var points = kvp.Value;
                    
                    if (_seriesVisibilityCheckBoxes.ContainsKey(address) && 
                        _seriesVisibilityCheckBoxes[address].IsChecked == true && points.Count > 0)
                    {
                        // Find closest point in time
                        var closestPoint = points.OrderBy(p => Math.Abs(p.TimeOffset - dataX)).FirstOrDefault();
                        if (closestPoint != null)
                        {
                            // Convert point to screen coordinates
                            var pointScreenX = leftMargin + ((closestPoint.TimeOffset - _minX) / xRange) * plotWidth;
                            var pointScreenY = topMargin + plotHeight - ((closestPoint.Value - _minY) / yRange) * plotHeight;
                            
                            // Calculate distance in pixels
                            var distance = Math.Sqrt(Math.Pow(mousePos.X - pointScreenX, 2) + Math.Pow(mousePos.Y - pointScreenY, 2));
                            
                            if (distance < proximityThreshold && distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestSeriesName = _seriesNameTextBoxes.ContainsKey(address) ? 
                                    _seriesNameTextBoxes[address].Text : $"Register {address}";
                            }
                        }
                    }
                }
                
                if (closestSeriesName != null)
                {
                    tooltipContent += $"\nSeries: {closestSeriesName}";
                }
                
                ShowTooltip(mousePos, tooltipContent);
            }
            else
            {
                HideTooltip();
            }
        }
        
        /// <summary>
        /// Show tooltip
        /// </summary>
        private void ShowTooltip(System.Windows.Point position, string content)
        {
            if (ChartCanvas == null) return;
            
            HideTooltip();
            
            _tooltipText = new TextBlock
            {
                Text = content,
                Background = System.Windows.Media.Brushes.LightYellow,
                Foreground = System.Windows.Media.Brushes.Black,
                Padding = new Thickness(5),
                FontSize = 10,
                IsHitTestVisible = false
            };
            
            Canvas.SetLeft(_tooltipText, position.X + 10);
            Canvas.SetTop(_tooltipText, position.Y - 30);
            
            ChartCanvas.Children.Add(_tooltipText);
        }
        
        /// <summary>
        /// Hide tooltip
        /// </summary>
        private void HideTooltip()
        {
            if (ChartCanvas != null && _tooltipText != null)
            {
                ChartCanvas.Children.Remove(_tooltipText);
                _tooltipText = null;
            }
        }
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Data point for charting
    /// </summary>
    public class ChartDataPoint
    {
        public double TimeOffset { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
