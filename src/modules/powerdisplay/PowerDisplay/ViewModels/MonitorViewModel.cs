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
using PowerDisplay.Configuration;
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
    private readonly SimpleDebouncer _brightnessDebouncer = new(AppConstants.UI.SliderDebounceDelayMs);
    private readonly SimpleDebouncer _contrastDebouncer = new(AppConstants.UI.SliderDebounceDelayMs);
    private readonly SimpleDebouncer _volumeDebouncer = new(AppConstants.UI.SliderDebounceDelayMs);

    private int _brightness;
    private int _contrast;
    private int _volume;
    private bool _isAvailable;

    // Visibility settings (controlled by Settings UI)
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
            case nameof(ColorTemperature):
                // Update underlying monitor model
                _monitor.CurrentColorTemperature = value;
                OnPropertyChanged(nameof(ColorTemperature));
                OnPropertyChanged(nameof(ColorTemperaturePresetName));
                break;
        }
    }

    /// <summary>
    /// Unified method to apply brightness with hardware update and state persistence.
    /// Can be called from Flyout UI (with debounce) or Settings UI/IPC (immediate).
    /// </summary>
    /// <param name="brightness">Brightness value (0-100)</param>
    /// <param name="immediate">If true, applies immediately; if false, debounces for smooth slider</param>
    /// <param name="fromProfile">If true, skip profile change detection (avoid recursion)</param>
    public async Task SetBrightnessAsync(int brightness, bool immediate = false, bool fromProfile = false)
    {
        brightness = Math.Clamp(brightness, MinBrightness, MaxBrightness);

        // Update UI state immediately for smooth response
        if (_brightness != brightness)
        {
            _brightness = brightness;
            OnPropertyChanged(nameof(Brightness));
        }

        // Apply to hardware (with or without debounce)
        if (immediate)
        {
            await ApplyBrightnessToHardwareAsync(brightness, fromProfile);
        }
        else
        {
            // Debounce for slider smoothness (always from user interaction, not from profile)
            var capturedValue = brightness;
            _brightnessDebouncer.Debounce(async () => await ApplyBrightnessToHardwareAsync(capturedValue, fromUserInteraction: true));
        }
    }

    /// <summary>
    /// Unified method to apply contrast with hardware update and state persistence.
    /// </summary>
    public async Task SetContrastAsync(int contrast, bool immediate = false, bool fromProfile = false)
    {
        contrast = Math.Clamp(contrast, MinContrast, MaxContrast);

        if (_contrast != contrast)
        {
            _contrast = contrast;
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(ContrastPercent));
        }

        if (immediate)
        {
            await ApplyContrastToHardwareAsync(contrast, fromProfile);
        }
        else
        {
            var capturedValue = contrast;
            _contrastDebouncer.Debounce(async () => await ApplyContrastToHardwareAsync(capturedValue, fromUserInteraction: true));
        }
    }

    /// <summary>
    /// Unified method to apply volume with hardware update and state persistence.
    /// </summary>
    public async Task SetVolumeAsync(int volume, bool immediate = false, bool fromProfile = false)
    {
        volume = Math.Clamp(volume, MinVolume, MaxVolume);

        if (_volume != volume)
        {
            _volume = volume;
            OnPropertyChanged(nameof(Volume));
        }

        if (immediate)
        {
            await ApplyVolumeToHardwareAsync(volume, fromProfile);
        }
        else
        {
            var capturedValue = volume;
            _volumeDebouncer.Debounce(async () => await ApplyVolumeToHardwareAsync(capturedValue, fromUserInteraction: true));
        }
    }

    /// <summary>
    /// Unified method to apply color temperature with hardware update and state persistence.
    /// Always immediate (no debouncing for discrete preset values).
    /// </summary>
    public async Task SetColorTemperatureAsync(int colorTemperature, bool fromProfile = false)
    {
        try
        {
            Logger.LogInfo($"[{HardwareId}] Setting color temperature to 0x{colorTemperature:X2}");

            var result = await _monitorManager.SetColorTemperatureAsync(Id, colorTemperature);

            if (result.IsSuccess)
            {
                _monitor.CurrentColorTemperature = colorTemperature;
                OnPropertyChanged(nameof(ColorTemperature));
                OnPropertyChanged(nameof(ColorTemperaturePresetName));

                _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, nameof(ColorTemperature), colorTemperature);

                // Trigger profile change detection if from user interaction
                if (!fromProfile)
                {
                    _mainViewModel?.OnMonitorParameterChanged(_monitor.HardwareId, nameof(ColorTemperature), colorTemperature);
                }

                Logger.LogInfo($"[{HardwareId}] Color temperature applied successfully");
            }
            else
            {
                Logger.LogWarning($"[{HardwareId}] Failed to set color temperature: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{HardwareId}] Exception setting color temperature: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method - applies brightness to hardware and persists state.
    /// Unified logic for all sources (Flyout, Settings, etc.).
    /// </summary>
    private async Task ApplyBrightnessToHardwareAsync(int brightness, bool fromUserInteraction = false)
    {
        try
        {
            Logger.LogDebug($"[{HardwareId}] Applying brightness: {brightness}%");

            var result = await _monitorManager.SetBrightnessAsync(Id, brightness);

            if (result.IsSuccess)
            {
                _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, nameof(Brightness), brightness);

                // Trigger profile change detection if from user interaction
                if (fromUserInteraction)
                {
                    _mainViewModel?.OnMonitorParameterChanged(_monitor.HardwareId, nameof(Brightness), brightness);
                }
            }
            else
            {
                Logger.LogWarning($"[{HardwareId}] Failed to set brightness: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{HardwareId}] Exception setting brightness: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method - applies contrast to hardware and persists state.
    /// </summary>
    private async Task ApplyContrastToHardwareAsync(int contrast, bool fromUserInteraction = false)
    {
        try
        {
            Logger.LogDebug($"[{HardwareId}] Applying contrast: {contrast}%");

            var result = await _monitorManager.SetContrastAsync(Id, contrast);

            if (result.IsSuccess)
            {
                _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, nameof(Contrast), contrast);

                // Trigger profile change detection if from user interaction
                if (fromUserInteraction)
                {
                    _mainViewModel?.OnMonitorParameterChanged(_monitor.HardwareId, nameof(Contrast), contrast);
                }
            }
            else
            {
                Logger.LogWarning($"[{HardwareId}] Failed to set contrast: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{HardwareId}] Exception setting contrast: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method - applies volume to hardware and persists state.
    /// </summary>
    private async Task ApplyVolumeToHardwareAsync(int volume, bool fromUserInteraction = false)
    {
        try
        {
            Logger.LogDebug($"[{HardwareId}] Applying volume: {volume}%");

            var result = await _monitorManager.SetVolumeAsync(Id, volume);

            if (result.IsSuccess)
            {
                _mainViewModel?.SaveMonitorSettingDirect(_monitor.HardwareId, nameof(Volume), volume);

                // Trigger profile change detection if from user interaction
                if (fromUserInteraction)
                {
                    _mainViewModel?.OnMonitorParameterChanged(_monitor.HardwareId, nameof(Volume), volume);
                }
            }
            else
            {
                Logger.LogWarning($"[{HardwareId}] Failed to set volume: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{HardwareId}] Exception setting volume: {ex.Message}");
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
        _showContrast = monitor.SupportsContrast;
        _showVolume = monitor.SupportsVolume;

        // Color temperature initialization removed - now controlled via Settings UI
        // The Monitor.CurrentColorTemperature stores VCP 0x14 preset value (e.g., 0x05 for 6500K)
        // and will be initialized by MonitorManager based on capabilities

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

    public string CommunicationMethod => _monitor.CommunicationMethod;

    public bool IsInternal => _monitor.CommunicationMethod == "WMI";

    public string? CapabilitiesRaw => _monitor.CapabilitiesRaw;

    public VcpCapabilities? VcpCapabilitiesInfo => _monitor.VcpCapabilitiesInfo;

    /// <summary>
    /// Gets the icon glyph based on communication method
    /// WMI monitors (laptop internal displays) use laptop icon, others use external monitor icon
    /// </summary>
    public string MonitorIconGlyph => _monitor.CommunicationMethod?.Contains("WMI", StringComparison.OrdinalIgnoreCase) == true
        ? "\uEA37" // Laptop icon for WMI
        : "\uE7F4"; // External monitor icon for DDC/CI and others

    // Monitor property ranges
    public int MinBrightness => _monitor.MinBrightness;

    public int MaxBrightness => _monitor.MaxBrightness;

    public int MinContrast => _monitor.MinContrast;

    public int MaxContrast => _monitor.MaxContrast;

    public int MinVolume => _monitor.MinVolume;

    public int MaxVolume => _monitor.MaxVolume;

    // Advanced control display logic
    public bool HasAdvancedControls => ShowContrast || ShowVolume;

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
                // Use unified method with debouncing for smooth slider
                _ = SetBrightnessAsync(value, immediate: false);
            }
        }
    }

    /// <summary>
    /// Color temperature VCP preset value (from VCP code 0x14).
    /// Read-only in flyout UI - controlled via Settings UI.
    /// Returns the raw VCP value (e.g., 0x05 for 6500K).
    /// </summary>
    public int ColorTemperature => _monitor.CurrentColorTemperature;

    /// <summary>
    /// Human-readable color temperature preset name (e.g., "6500K", "sRGB")
    /// </summary>
    public string ColorTemperaturePresetName => _monitor.ColorTemperaturePresetName;

    public int Contrast
    {
        get => _contrast;
        set
        {
            if (_contrast != value)
            {
                // Use unified method with debouncing
                _ = SetContrastAsync(value, immediate: false);
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
                // Use unified method with debouncing
                _ = SetVolumeAsync(value, immediate: false);
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

    // SetColorTemperatureCommand removed - now controlled via Settings UI
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
    // ColorTemperaturePercent removed - now controlled via Settings UI
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
        if (propertyName == nameof(Contrast))
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
        _contrastDebouncer?.Dispose();
        _volumeDebouncer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
