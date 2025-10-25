// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.UI.Xaml;
using PowerDisplay.Commands;
using PowerDisplay.Core;
using PowerDisplay.Core.Models;
using PowerDisplay.Helpers;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.ViewModels;

/// <summary>
/// ViewModel for individual monitor
/// </summary>
public partial class MonitorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Monitor _monitor;
    private readonly MonitorManager _monitorManager;
    private readonly MainViewModel? _mainViewModel;

    // Simple debouncers for each property (KISS principle - simpler than complex queue)
    private readonly SimpleDebouncer _brightnessDebouncer = new(300);
    private readonly SimpleDebouncer _colorTempDebouncer = new(300);
    private readonly SimpleDebouncer _contrastDebouncer = new(300);
    private readonly SimpleDebouncer _volumeDebouncer = new(300);

    private int _brightness;
    private int _colorTemperature;
    private int _contrast;
    private int _volume;
    private bool _isAvailable;

    // Visibility settings (controlled by Settings UI)
    private bool _showColorTemperature;
    private bool _showContrast;
    private bool _showVolume;

    /// <summary>
    /// Updates a property value directly without triggering hardware updates.
    /// Used during initialization to update UI from saved state.
    /// </summary>
    internal void UpdatePropertySilently(string propertyName, int value)
    {
        switch (propertyName)
        {
            case nameof(ColorTemperature):
                _colorTemperature = value;
                OnPropertyChanged(nameof(ColorTemperature));
                OnPropertyChanged(nameof(ColorTemperaturePercent));
                break;
            case nameof(Brightness):
                _brightness = value;
                OnPropertyChanged(nameof(Brightness));
                break;
            case nameof(Contrast):
                _contrast = value;
                OnPropertyChanged(nameof(Contrast));
                OnPropertyChanged(nameof(ContrastPercent));
                break;
            case nameof(Volume):
                _volume = value;
                OnPropertyChanged(nameof(Volume));
                break;
        }
    }

    // Conversion function for x:Bind (AOT-compatible alternative to converters)
    public Visibility ConvertBoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    // Property to access IsInteractionEnabled from parent ViewModel
    public bool IsInteractionEnabled => _mainViewModel?.IsInteractionEnabled ?? true;

    public MonitorViewModel(Monitor monitor, MonitorManager monitorManager, MainViewModel mainViewModel)
    {
        _monitor = monitor;
        _monitorManager = monitorManager;
        _mainViewModel = mainViewModel;

        // Subscribe to MainViewModel property changes to update IsInteractionEnabled
        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        }

        // Initialize Show properties based on hardware capabilities
        _showColorTemperature = monitor.SupportsColorTemperature; // Only show for DDC/CI monitors that support it
        _showContrast = monitor.SupportsContrast;
        _showVolume = monitor.SupportsVolume;

        // Try to get current color temperature via DDC/CI, use default if failed
        try
        {
            // For DDC/CI monitors that support color temperature, use 6500K as default
            // The actual temperature will be loaded asynchronously after construction
            if (monitor.SupportsColorTemperature)
            {
                _colorTemperature = 6500; // Default neutral temperature for DDC monitors
            }
            else
            {
                _colorTemperature = 6500; // Default for unsupported monitors
            }

            monitor.CurrentColorTemperature = _colorTemperature;
            Logger.LogDebug($"Initialized {monitor.Id} with default color temperature {_colorTemperature}K");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to initialize color temperature for {monitor.Id}: {ex.Message}");
            _colorTemperature = 6500; // Default neutral temperature
            monitor.CurrentColorTemperature = 6500;
        }

        // Initialize basic properties from monitor
        _brightness = monitor.CurrentBrightness;
        _contrast = monitor.CurrentContrast;
        _volume = monitor.CurrentVolume;
        _isAvailable = monitor.IsAvailable;
    }

    public string Id => _monitor.Id;

    public string HardwareId => _monitor.HardwareId;

    public string Name => _monitor.Name;

    public string Manufacturer => _monitor.Manufacturer;

    public MonitorType Type => _monitor.Type;

    public string TypeDisplay => Type == MonitorType.Internal ? "Internal" : "External";

    /// <summary>
    /// Gets the icon glyph based on monitor type
    /// </summary>
    public string MonitorIconGlyph => Type == MonitorType.Internal ? "\uEA37" : "\uE7F4";

    // Monitor property ranges
    public int MinBrightness => _monitor.MinBrightness;

    public int MaxBrightness => _monitor.MaxBrightness;

    public int MinColorTemperature => _monitor.MinColorTemperature;

    public int MaxColorTemperature => _monitor.MaxColorTemperature;

    public int MinContrast => _monitor.MinContrast;

    public int MaxContrast => _monitor.MaxContrast;

    public int MinVolume => _monitor.MinVolume;

    public int MaxVolume => _monitor.MaxVolume;

    // Advanced control display logic
    public bool HasAdvancedControls => ShowColorTemperature || ShowContrast || ShowVolume;

    public bool ShowColorTemperature
    {
        get => _showColorTemperature;
        set
        {
            if (_showColorTemperature != value)
            {
                _showColorTemperature = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAdvancedControls));
            }
        }
    }

    public bool ShowContrast
    {
        get => _showContrast;
        set
        {
            if (_showContrast != value)
            {
                _showContrast = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAdvancedControls));
            }
        }
    }

    public bool ShowVolume
    {
        get => _showVolume;
        set
        {
            if (_showVolume != value)
            {
                _showVolume = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAdvancedControls));
            }
        }
    }

    public int Brightness
    {
        get => _brightness;
        set
        {
            if (_brightness != value)
            {
                // Update UI state immediately - keep slider smooth
                _brightness = value;
                OnPropertyChanged(); // UI responds immediately

                // Debounce hardware update - much simpler than complex queue!
                var capturedValue = value; // Capture value for async closure
                _brightnessDebouncer.Debounce(async () =>
                {
                    try
                    {
                        await _monitorManager.SetBrightnessAsync(Id, capturedValue);
                        _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, "Brightness", capturedValue);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to set brightness for {Id}: {ex.Message}");
                    }
                });
            }
        }
    }

    public int ColorTemperature
    {
        get => _colorTemperature;
        set
        {
            if (_colorTemperature != value)
            {
                _colorTemperature = value;
                OnPropertyChanged();

                // Debounce hardware update - simple and clean!
                var capturedValue = value;
                _colorTempDebouncer.Debounce(async () =>
                {
                    try
                    {
                        var result = await _monitorManager.SetColorTemperatureAsync(Id, capturedValue);
                        if (result.IsSuccess)
                        {
                            _monitor.CurrentColorTemperature = capturedValue;
                            _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, "ColorTemperature", capturedValue);
                        }
                        else
                        {
                            Logger.LogError($"[{Id}] Failed to set color temperature: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to set color temperature for {Id}: {ex.Message}");
                    }
                });
            }
        }
    }

    public int Contrast
    {
        get => _contrast;
        set
        {
            if (_contrast != value)
            {
                _contrast = value;
                OnPropertyChanged();

                // Debounce hardware update
                var capturedValue = value;
                _contrastDebouncer.Debounce(async () =>
                {
                    try
                    {
                        await _monitorManager.SetContrastAsync(Id, capturedValue);
                        _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, "Contrast", capturedValue);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to set contrast for {Id}: {ex.Message}");
                    }
                });
            }
        }
    }

    public int Volume
    {
        get => _volume;
        set
        {
            if (_volume != value)
            {
                _volume = value;
                OnPropertyChanged();

                // Debounce hardware update
                var capturedValue = value;
                _volumeDebouncer.Debounce(async () =>
                {
                    try
                    {
                        await _monitorManager.SetVolumeAsync(Id, capturedValue);
                        _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, "Volume", capturedValue);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to set volume for {Id}: {ex.Message}");
                    }
                });
            }
        }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            _isAvailable = value;
            OnPropertyChanged();
        }
    }

    public ICommand SetBrightnessCommand => new RelayCommand<int?>((brightness) =>
    {
        if (brightness.HasValue)
        {
            Brightness = brightness.Value;
        }
    });

    public ICommand SetColorTemperatureCommand => new RelayCommand<int?>((temperature) =>
    {
        if (temperature.HasValue && _monitor.SupportsColorTemperature)
        {
            Logger.LogDebug($"[{Id}] Color temperature command: {temperature.Value}K (DDC/CI)");
            ColorTemperature = temperature.Value;
        }
        else if (temperature.HasValue && !_monitor.SupportsColorTemperature)
        {
            Logger.LogWarning($"[{Id}] Color temperature not supported on this monitor");
        }
    });

    public ICommand SetContrastCommand => new RelayCommand<int?>((contrast) =>
    {
        if (contrast.HasValue)
        {
            Contrast = contrast.Value;
        }
    });

    public ICommand SetVolumeCommand => new RelayCommand<int?>((volume) =>
    {
        if (volume.HasValue)
        {
            Volume = volume.Value;
        }
    });

    // Percentage-based properties for uniform slider behavior
    public int ColorTemperaturePercent
    {
        get => MapToPercent(_colorTemperature, MinColorTemperature, MaxColorTemperature);
        set
        {
            var actualValue = MapFromPercent(value, MinColorTemperature, MaxColorTemperature);
            ColorTemperature = actualValue;
        }
    }

    public int ContrastPercent
    {
        get => MapToPercent(_contrast, MinContrast, MaxContrast);
        set
        {
            var actualValue = MapFromPercent(value, MinContrast, MaxContrast);
            Contrast = actualValue;
        }
    }

    // Mapping functions for percentage conversion
    private int MapToPercent(int value, int min, int max)
    {
        if (max <= min)
        {
            return 0;
        }

        return (int)Math.Round((value - min) * 100.0 / (max - min));
    }

    private int MapFromPercent(int percent, int min, int max)
    {
        if (max <= min)
        {
            return min;
        }

        percent = Math.Clamp(percent, 0, 100);
        return min + (int)Math.Round(percent * (max - min) / 100.0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Notify percentage properties when actual values change
        if (propertyName == nameof(ColorTemperature))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorTemperaturePercent)));
        }
        else if (propertyName == nameof(Contrast))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContrastPercent)));
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsInteractionEnabled))
        {
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
    }

    public void Dispose()
    {
        // Unsubscribe from MainViewModel events
        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        }

        // Dispose all debouncers
        _brightnessDebouncer?.Dispose();
        _colorTempDebouncer?.Dispose();
        _contrastDebouncer?.Dispose();
        _volumeDebouncer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
