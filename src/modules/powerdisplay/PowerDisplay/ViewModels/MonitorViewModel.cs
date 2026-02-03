// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.UI.Xaml;

using PowerDisplay.Common.Models;
using PowerDisplay.Configuration;
using PowerDisplay.Helpers;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.ViewModels;

/// <summary>
/// ViewModel for individual monitor
/// </summary>
public partial class MonitorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Monitor _monitor;
    private readonly MonitorManager _monitorManager;
    private readonly MainViewModel? _mainViewModel;

    private int _brightness;
    private int _contrast;
    private int _volume;
    private bool _isAvailable;

    // Visibility settings (controlled by Settings UI)
    private bool _showContrast;
    private bool _showVolume;
    private bool _showInputSource;
    private bool _showRotation;
    private bool _showPowerState;

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
    /// Apply brightness with hardware update and state persistence.
    /// </summary>
    /// <param name="brightness">Brightness value (0-100)</param>
    public async Task SetBrightnessAsync(int brightness)
    {
        brightness = Math.Clamp(brightness, MinBrightness, MaxBrightness);

        // Update UI state immediately
        if (_brightness != brightness)
        {
            _brightness = brightness;
            OnPropertyChanged(nameof(Brightness));
        }

        // Apply to hardware
        await ApplyPropertyToHardwareAsync(nameof(Brightness), brightness, _monitorManager.SetBrightnessAsync);
    }

    /// <summary>
    /// Apply contrast with hardware update and state persistence.
    /// </summary>
    public async Task SetContrastAsync(int contrast)
    {
        contrast = Math.Clamp(contrast, MinContrast, MaxContrast);

        if (_contrast != contrast)
        {
            _contrast = contrast;
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(ContrastPercent));
        }

        await ApplyPropertyToHardwareAsync(nameof(Contrast), contrast, _monitorManager.SetContrastAsync);
    }

    /// <summary>
    /// Apply volume with hardware update and state persistence.
    /// </summary>
    public async Task SetVolumeAsync(int volume)
    {
        volume = Math.Clamp(volume, MinVolume, MaxVolume);

        if (_volume != volume)
        {
            _volume = volume;
            OnPropertyChanged(nameof(Volume));
        }

        await ApplyPropertyToHardwareAsync(nameof(Volume), volume, _monitorManager.SetVolumeAsync);
    }

    /// <summary>
    /// Unified method to apply color temperature with hardware update and state persistence.
    /// Always immediate (no debouncing for discrete preset values).
    /// </summary>
    public async Task SetColorTemperatureAsync(int colorTemperature)
    {
        try
        {
            var result = await _monitorManager.SetColorTemperatureAsync(Id, colorTemperature);

            if (result.IsSuccess)
            {
                _monitor.CurrentColorTemperature = colorTemperature;
                OnPropertyChanged(nameof(ColorTemperature));
                OnPropertyChanged(nameof(ColorTemperaturePresetName));

                // Refresh the color presets list to update IsSelected checkmarks in UI
                RefreshAvailableColorPresets();

                _mainViewModel?.SaveMonitorSettingDirect(_monitor.Id, nameof(ColorTemperature), colorTemperature);
            }
            else
            {
                Logger.LogWarning($"[{Id}] Failed to set color temperature: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{Id}] Exception setting color temperature: {ex.Message}");
        }
    }

    /// <summary>
    /// Generic method to apply a monitor property to hardware and persist state.
    /// Consolidates common logic for brightness, contrast, and volume operations.
    /// </summary>
    /// <param name="propertyName">Name of the property being set (for logging and state persistence)</param>
    /// <param name="value">Value to apply</param>
    /// <param name="setAsyncFunc">Async function to call on MonitorManager</param>
    private async Task ApplyPropertyToHardwareAsync(
        string propertyName,
        int value,
        Func<string, int, CancellationToken, Task<MonitorOperationResult>> setAsyncFunc)
    {
        try
        {
            var result = await setAsyncFunc(Id, value, default);

            if (result.IsSuccess)
            {
                _mainViewModel?.SaveMonitorSettingDirect(_monitor.Id, propertyName, value);
            }
            else
            {
                Logger.LogWarning($"[{Id}] Failed to set {propertyName.ToLowerInvariant()}: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{Id}] Exception setting {propertyName.ToLowerInvariant()}: {ex.Message}");
        }
    }

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

        // Subscribe to underlying Monitor property changes (e.g., Orientation updates in mirror mode)
        _monitor.PropertyChanged += OnMonitorPropertyChanged;

        // Initialize Show properties based on hardware capabilities
        _showContrast = monitor.SupportsContrast;
        _showVolume = monitor.SupportsVolume;
        _showInputSource = monitor.SupportsInputSource;
        _showPowerState = monitor.SupportsPowerState;
        _showColorTemperature = monitor.SupportsColorTemperature;

        // Initialize basic properties from monitor
        _brightness = monitor.CurrentBrightness;
        _contrast = monitor.CurrentContrast;
        _volume = monitor.CurrentVolume;
        _isAvailable = monitor.IsAvailable;
    }

    public string Id => _monitor.Id;

    public string Name => _monitor.Name;

    /// <summary>
    /// Gets the monitor number from the underlying monitor model (Windows DISPLAY number)
    /// </summary>
    public int MonitorNumber => _monitor.MonitorNumber;

    /// <summary>
    /// Gets the display name - includes monitor number when multiple monitors exist.
    /// Follows the same logic as Settings UI's MonitorInfo.DisplayName for consistency.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var monitorCount = _mainViewModel?.Monitors?.Count ?? 0;

            // Show monitor number only when there are multiple monitors and MonitorNumber is valid
            if (monitorCount > 1 && MonitorNumber > 0)
            {
                return $"{Name} {MonitorNumber}";
            }

            return Name;
        }
    }

    public string CommunicationMethod => _monitor.CommunicationMethod;

    public bool IsInternal => _monitor.CommunicationMethod == "WMI";

    public string? CapabilitiesRaw => _monitor.CapabilitiesRaw;

    public VcpCapabilities? VcpCapabilitiesInfo => _monitor.VcpCapabilitiesInfo;

    /// <summary>
    /// Gets the icon glyph based on communication method
    /// WMI monitors (laptop internal displays) use laptop icon, others use external monitor icon
    /// </summary>
    public string MonitorIconGlyph => _monitor.CommunicationMethod?.Contains("WMI", StringComparison.OrdinalIgnoreCase) == true
        ? AppConstants.UI.InternalMonitorGlyph // Laptop icon for WMI
        : AppConstants.UI.ExternalMonitorGlyph; // External monitor icon for DDC/CI and others

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

    public bool ShowInputSource
    {
        get => _showInputSource;
        set
        {
            if (_showInputSource != value)
            {
                _showInputSource = value;
                OnPropertyChanged();
                OnMoreButtonPropertiesChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to show power state control in the More Button flyout.
    /// </summary>
    public bool ShowPowerState
    {
        get => _showPowerState && SupportsPowerState;
        set
        {
            if (_showPowerState != value)
            {
                _showPowerState = value;
                OnPropertyChanged();
                OnMoreButtonPropertiesChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the More Button should be visible.
    /// Visible when at least one feature (InputSource or PowerState) is enabled.
    /// </summary>
    public bool ShowMoreButton => ShowInputSource || ShowPowerState;

    /// <summary>
    /// Gets a value indicating whether to show separator after Input Source section.
    /// Only shown when both InputSource and PowerState are visible.
    /// </summary>
    public bool ShowSeparatorAfterInputSource => ShowInputSource && ShowPowerState;

    /// <summary>
    /// Notifies property changes for More Button related properties.
    /// </summary>
    private void OnMoreButtonPropertiesChanged()
    {
        OnPropertyChanged(nameof(ShowMoreButton));
        OnPropertyChanged(nameof(ShowSeparatorAfterInputSource));
    }

    /// <summary>
    /// Gets or sets a value indicating whether to show rotation controls (controlled by Settings UI, default false).
    /// </summary>
    public bool ShowRotation
    {
        get => _showRotation;
        set
        {
            if (_showRotation != value)
            {
                _showRotation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current rotation/orientation of the monitor (0=normal, 1=90°, 2=180°, 3=270°)
    /// </summary>
    public int CurrentRotation => _monitor.Orientation;

    /// <summary>
    /// Gets a value indicating whether the current rotation is 0° (normal/default).
    /// </summary>
    public bool IsRotation0 => CurrentRotation == 0;

    /// <summary>
    /// Gets a value indicating whether the current rotation is 90° (rotated right).
    /// </summary>
    public bool IsRotation1 => CurrentRotation == 1;

    /// <summary>
    /// Gets a value indicating whether the current rotation is 180° (inverted).
    /// </summary>
    public bool IsRotation2 => CurrentRotation == 2;

    /// <summary>
    /// Gets a value indicating whether the current rotation is 270° (rotated left).
    /// </summary>
    public bool IsRotation3 => CurrentRotation == 3;

    /// <summary>
    /// Set rotation/orientation for this monitor.
    /// Note: MonitorManager.SetRotationAsync will refresh all monitors' orientations after success,
    /// which triggers PropertyChanged through OnMonitorPropertyChanged - no manual notification needed here.
    /// </summary>
    /// <param name="orientation">Orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
    public async Task SetRotationAsync(int orientation)
    {
        // Validate orientation range (0=normal, 1=90°, 2=180°, 3=270°)
        if (orientation < 0 || orientation > 3)
        {
            return;
        }

        // If already at this orientation, do nothing
        if (CurrentRotation == orientation)
        {
            return;
        }

        try
        {
            var result = await _monitorManager.SetRotationAsync(Id, orientation);

            if (!result.IsSuccess)
            {
                Logger.LogWarning($"[{Id}] Failed to set rotation: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{Id}] Exception setting rotation: {ex.Message}");
        }
    }

    public int Brightness
    {
        get => _brightness;
        set
        {
            if (_brightness != value)
            {
                _ = SetBrightnessAsync(value);
            }
        }
    }

    /// <summary>
    /// Gets color temperature VCP preset value (from VCP code 0x14).
    /// Read-only in flyout UI - controlled via Settings UI.
    /// Returns the raw VCP value (e.g., 0x05 for 6500K).
    /// </summary>
    public int ColorTemperature => _monitor.CurrentColorTemperature;

    /// <summary>
    /// Gets human-readable color temperature preset name (e.g., "6500K", "sRGB")
    /// </summary>
    public string ColorTemperaturePresetName => _monitor.ColorTemperaturePresetName;

    /// <summary>
    /// Gets a value indicating whether this monitor supports color temperature via VCP 0x14
    /// </summary>
    public bool SupportsColorTemperature => _monitor.SupportsColorTemperature;

    private List<ColorTemperatureItem>? _availableColorPresets;
    private bool _showColorTemperature;

    /// <summary>
    /// Gets or sets a value indicating whether to show color temperature switcher (controlled by Settings UI, default false).
    /// </summary>
    public bool ShowColorTemperature
    {
        get => _showColorTemperature && SupportsColorTemperature;
        set
        {
            if (_showColorTemperature != value)
            {
                _showColorTemperature = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets available color temperature presets for this monitor
    /// </summary>
    public List<ColorTemperatureItem>? AvailableColorPresets
    {
        get
        {
            if (_availableColorPresets == null && SupportsColorTemperature)
            {
                RefreshAvailableColorPresets();
            }

            return _availableColorPresets;
        }
    }

    /// <summary>
    /// Standard MCCS color temperature presets (VCP 0x14 values) to use as fallback
    /// when the monitor doesn't report discrete values in its capabilities string.
    /// </summary>
    private static readonly int[] StandardColorTemperaturePresets = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0B };

    /// <summary>
    /// Refresh the list of available color temperature presets based on monitor capabilities
    /// </summary>
    private void RefreshAvailableColorPresets()
    {
        if (!SupportsColorTemperature)
        {
            _availableColorPresets = null;
            return;
        }

        IEnumerable<int> presetValues;
        var vcpInfo = VcpCapabilitiesInfo;

        // Try to get discrete values from capabilities string
        if (vcpInfo != null &&
            vcpInfo.SupportedVcpCodes.TryGetValue(0x14, out var colorTempInfo) &&
            colorTempInfo.HasDiscreteValues &&
            colorTempInfo.SupportedValues.Count > 0)
        {
            // Use values from capabilities string
            presetValues = colorTempInfo.SupportedValues;
        }
        else
        {
            // Fallback to standard MCCS presets when capabilities don't list discrete values
            presetValues = StandardColorTemperaturePresets;
        }

        _availableColorPresets = presetValues.Select(value => new ColorTemperatureItem
        {
            VcpValue = value,
            DisplayName = Common.Utils.VcpNames.GetFormattedValueName(0x14, value),
            IsSelected = value == _monitor.CurrentColorTemperature,
            MonitorId = _monitor.Id,
        }).ToList();

        OnPropertyChanged(nameof(AvailableColorPresets));
    }

    /// <summary>
    /// Gets a value indicating whether this monitor supports input source switching via VCP 0x60
    /// </summary>
    public bool SupportsInputSource => _monitor.SupportsInputSource;

    /// <summary>
    /// Gets current input source VCP value (from VCP code 0x60)
    /// </summary>
    public int CurrentInputSource => _monitor.CurrentInputSource;

    /// <summary>
    /// Gets human-readable current input source name (e.g., "HDMI-1", "DisplayPort-1")
    /// </summary>
    public string CurrentInputSourceName => _monitor.InputSourceName;

    private List<InputSourceItem>? _availableInputSources;

    /// <summary>
    /// Gets available input sources for this monitor
    /// </summary>
    public List<InputSourceItem>? AvailableInputSources
    {
        get
        {
            if (_availableInputSources == null && SupportsInputSource)
            {
                RefreshAvailableInputSources();
            }

            return _availableInputSources;
        }
    }

    /// <summary>
    /// Refresh the list of available input sources based on monitor capabilities
    /// </summary>
    private void RefreshAvailableInputSources()
    {
        var supportedSources = _monitor.SupportedInputSources;
        if (supportedSources == null || supportedSources.Count == 0)
        {
            _availableInputSources = null;
            return;
        }

        _availableInputSources = supportedSources.Select(value => new InputSourceItem
        {
            Value = value,
            Name = Common.Utils.VcpNames.GetValueName(0x60, value) ?? $"Source 0x{value:X2}",
            SelectionVisibility = value == _monitor.CurrentInputSource ? Visibility.Visible : Visibility.Collapsed,
            MonitorId = _monitor.Id,
        }).ToList();

        OnPropertyChanged(nameof(AvailableInputSources));
    }

    /// <summary>
    /// Set input source for this monitor
    /// </summary>
    public async Task SetInputSourceAsync(int inputSource)
    {
        try
        {
            var result = await _monitorManager.SetInputSourceAsync(Id, inputSource);

            if (result.IsSuccess)
            {
                OnPropertyChanged(nameof(CurrentInputSource));
                OnPropertyChanged(nameof(CurrentInputSourceName));
                RefreshAvailableInputSources();
            }
            else
            {
                Logger.LogWarning($"[{Id}] Failed to set input source: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{Id}] Exception setting input source: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to set input source
    /// </summary>
    [RelayCommand]
    private async Task SetInputSource(int? source)
    {
        if (source.HasValue)
        {
            await SetInputSourceAsync(source.Value);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this monitor supports power state control via VCP 0xD6
    /// </summary>
    public bool SupportsPowerState => _monitor.SupportsPowerState;

    private List<PowerStateItem>? _availablePowerStates;

    /// <summary>
    /// Gets available power states for this monitor.
    /// The current power state is shown as selected based on the monitor's actual state.
    /// </summary>
    public List<PowerStateItem>? AvailablePowerStates
    {
        get
        {
            if (_availablePowerStates == null && SupportsPowerState)
            {
                RefreshAvailablePowerStates();
            }

            return _availablePowerStates;
        }
    }

    /// <summary>
    /// Refresh the list of available power states based on monitor capabilities
    /// </summary>
    private void RefreshAvailablePowerStates()
    {
        var supportedStates = _monitor.SupportedPowerStates;
        if (supportedStates == null || supportedStates.Count == 0)
        {
            _availablePowerStates = null;
            return;
        }

        _availablePowerStates = supportedStates.Select(value => new PowerStateItem
        {
            Value = value,
            Name = Common.Utils.VcpNames.GetValueName(0xD6, value) ?? $"State 0x{value:X2}",
            IsSelected = value == _monitor.CurrentPowerState,
            MonitorId = _monitor.Id,
        }).ToList();

        OnPropertyChanged(nameof(AvailablePowerStates));
    }

    /// <summary>
    /// Set power state for this monitor.
    /// Note: Setting any state other than "On" will turn off the display.
    /// </summary>
    public async Task SetPowerStateAsync(int powerState)
    {
        try
        {
            var result = await _monitorManager.SetPowerStateAsync(Id, powerState);

            if (result.IsSuccess)
            {
                // Update the model's power state and refresh UI
                _monitor.CurrentPowerState = powerState;
                RefreshAvailablePowerStates();
            }
            else
            {
                Logger.LogWarning($"[{Id}] Failed to set power state: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{Id}] Exception setting power state: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to set power state
    /// </summary>
    [RelayCommand]
    private async Task SetPowerState(int? state)
    {
        if (state.HasValue)
        {
            await SetPowerStateAsync(state.Value);
        }
    }

    public int Contrast
    {
        get => _contrast;
        set
        {
            if (_contrast != value)
            {
                _ = SetContrastAsync(value);
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
                _ = SetVolumeAsync(value);
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

    [RelayCommand]
    private void SetBrightness(int? brightness)
    {
        if (brightness.HasValue)
        {
            Brightness = brightness.Value;
        }
    }

    [RelayCommand]
    private void SetContrast(int? contrast)
    {
        if (contrast.HasValue)
        {
            Contrast = contrast.Value;
        }
    }

    [RelayCommand]
    private void SetVolume(int? volume)
    {
        if (volume.HasValue)
        {
            Volume = volume.Value;
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
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsInteractionEnabled))
        {
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
        else if (e.PropertyName == nameof(MainViewModel.HasMonitors))
        {
            // Monitor count changed, update display name to show/hide number suffix
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward Orientation changes from underlying Monitor to ViewModel properties
        // This is important for mirror mode where MonitorManager.RefreshAllOrientations()
        // updates multiple monitors sharing the same GdiDeviceName
        if (e.PropertyName == nameof(Monitor.Orientation))
        {
            OnPropertyChanged(nameof(CurrentRotation));
            OnPropertyChanged(nameof(IsRotation0));
            OnPropertyChanged(nameof(IsRotation1));
            OnPropertyChanged(nameof(IsRotation2));
            OnPropertyChanged(nameof(IsRotation3));
        }
    }

    public void Dispose()
    {
        // Unsubscribe from MainViewModel events
        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        }

        // Unsubscribe from underlying Monitor events
        _monitor.PropertyChanged -= OnMonitorPropertyChanged;

        GC.SuppressFinalize(this);
    }
}
