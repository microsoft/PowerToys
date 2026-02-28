// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Helpers;
using PowerDisplay.PowerDisplayXAML;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Main ViewModel for the PowerDisplay application.
/// Split into partial classes for better maintainability:
/// - MainViewModel.cs: Core properties, construction, and disposal
/// - MainViewModel.Monitors.cs: Monitor discovery and management
/// - MainViewModel.Settings.cs: Settings UI synchronization and profiles
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
public partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    private const int MdtEffectiveDpi = 0;
    private const int DefaultDpi = 96;

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    private readonly MonitorManager _monitorManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly SettingsUtils _settingsUtils;
    private readonly MonitorStateManager _stateManager;
    private readonly DisplayChangeWatcher _displayChangeWatcher;

    private ObservableCollection<MonitorViewModel> _monitors;
    private ObservableCollection<PowerDisplayProfile> _profiles;
    private bool _isScanning;
    private bool _isInitialized;
    private bool _isLoading;

    /// <summary>
    /// Event triggered when UI refresh is requested due to settings changes
    /// </summary>
    public event EventHandler? UIRefreshRequested;

    /// <summary>
    /// Event triggered when initial monitor discovery is completed.
    /// Used by MainWindow to know when data is ready for display.
    /// </summary>
    public event EventHandler? InitializationCompleted;

    public MainViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _cancellationTokenSource = new CancellationTokenSource();
        _monitors = new ObservableCollection<MonitorViewModel>();
        _profiles = new ObservableCollection<PowerDisplayProfile>();
        _isScanning = true;

        // Initialize settings utils
        _settingsUtils = SettingsUtils.Default;
        _stateManager = new MonitorStateManager();

        // Initialize the monitor manager
        _monitorManager = new MonitorManager();

        // Load profiles for quick apply feature
        LoadProfiles();

        // Load UI display settings (profile switcher, identify button, color temp switcher)
        LoadUIDisplaySettings();

        // Initialize display change watcher for auto-refresh on monitor plug/unplug
        // Use MonitorRefreshDelay from settings to allow hardware to stabilize after plug/unplug
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        int delaySeconds = Math.Clamp(settings?.Properties?.MonitorRefreshDelay ?? 5, 1, 30);
        _displayChangeWatcher = new DisplayChangeWatcher(_dispatcherQueue, TimeSpan.FromSeconds(delaySeconds));
        _displayChangeWatcher.DisplayChanged += OnDisplayChanged;

        // Start initial discovery
        _ = InitializeAsync(_cancellationTokenSource.Token);
    }

    public ObservableCollection<MonitorViewModel> Monitors
    {
        get => _monitors;
        set
        {
            _monitors = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<PowerDisplayProfile> Profiles
    {
        get => _profiles;
        set
        {
            _profiles = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProfiles));
        }
    }

    public bool HasProfiles => Profiles.Count > 0;

    // UI display control properties - loaded from settings
    private bool _showProfileSwitcher = true;
    private bool _showIdentifyMonitorsButton = true;

    /// <summary>
    /// Gets a value indicating whether to show the profile switcher button.
    /// Combines settings value with HasProfiles check.
    /// </summary>
    public bool ShowProfileSwitcherButton => _showProfileSwitcher && HasProfiles;

    /// <summary>
    /// Gets or sets a value indicating whether to show the profile switcher (from settings).
    /// </summary>
    public bool ShowProfileSwitcher
    {
        get => _showProfileSwitcher;
        set
        {
            if (_showProfileSwitcher != value)
            {
                _showProfileSwitcher = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowProfileSwitcherButton));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to show the identify monitors button.
    /// </summary>
    public bool ShowIdentifyMonitorsButton
    {
        get => _showIdentifyMonitorsButton;
        set
        {
            if (_showIdentifyMonitorsButton != value)
            {
                _showIdentifyMonitorsButton = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (_isScanning != value)
            {
                _isScanning = value;
                OnPropertyChanged();

                // Dependent properties that change with IsScanning
                OnPropertyChanged(nameof(HasMonitors));
                OnPropertyChanged(nameof(ShowNoMonitorsMessage));
                OnPropertyChanged(nameof(IsInteractionEnabled));
            }
        }
    }

    public bool HasMonitors => !IsScanning && Monitors.Count > 0;

    public bool ShowNoMonitorsMessage => !IsScanning && Monitors.Count == 0;

    public bool IsInitialized
    {
        get => _isInitialized;
        private set
        {
            _isInitialized = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
    }

    /// <summary>
    /// Gets a value indicating whether user interaction is enabled (not loading or scanning).
    /// </summary>
    public bool IsInteractionEnabled => !IsLoading && !IsScanning;

    [RelayCommand]
    private async Task RefreshAsync() => await RefreshMonitorsAsync();

    [RelayCommand]
    private unsafe void IdentifyMonitors()
    {
        try
        {
            // Get all monitor info from QueryDisplayConfig
            var allDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo().Values.ToList();

            // Build GDI name to MonitorNumber(s) mapping
            // Note: In mirror mode, multiple monitors may share the same GdiDeviceName
            var gdiToMonitorNumbers = allDisplayInfo
                .Where(info => info.MonitorNumber > 0)
                .GroupBy(info => info.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(info => info.MonitorNumber).Distinct().OrderBy(n => n).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            // Enumerate all HMONITORs using Win32 EnumDisplayMonitors.
            // This avoids DisplayArea.FindAll() which can throw InvalidCastException
            // due to WinRT IVectorView→IReadOnlyList projection issues.
            var hMonitors = new List<IntPtr>();
            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (hMonitor, _, _, _) =>
                {
                    hMonitors.Add(hMonitor);
                    return true;
                },
                IntPtr.Zero);

            // For each HMONITOR, get GDI device name to find MonitorNumber(s)
            int windowsCreated = 0;
            foreach (var hMonitor in hMonitors)
            {
                // Get GDI device name and work area from HMONITOR
                var monitorInfo = new MonitorInfoEx { CbSize = (uint)sizeof(MonitorInfoEx) };
                if (!GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    continue;
                }

                var gdiDeviceName = monitorInfo.GetDeviceName();

                // Look up MonitorNumber(s) by GDI device name
                if (!gdiToMonitorNumbers.TryGetValue(gdiDeviceName, out var monitorNumbers) || monitorNumbers.Count == 0)
                {
                    continue;
                }

                // Format display text: single number for normal mode, "1|2" for mirror mode
                var displayText = string.Join("|", monitorNumbers);

                // Convert Win32 RECT work area to WinUI RectInt32
                var rcWork = monitorInfo.RcWork;
                var workArea = new Windows.Graphics.RectInt32(rcWork.Left, rcWork.Top, rcWork.Width, rcWork.Height);

                // Get DPI for this monitor (same pattern as CmdPal's GetDpiForDisplay)
                int hr = GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out uint dpiX, out _);
                int dpi = (hr == 0 && dpiX > 0) ? (int)dpiX : DefaultDpi;

                // Create and position identify window
                var identifyWindow = new IdentifyWindow(displayText);
                identifyWindow.PositionOnDisplay(workArea, dpi);
                identifyWindow.Activate();
                windowsCreated++;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to identify monitors: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplyProfile(PowerDisplayProfile? profile)
    {
        if (profile != null && profile.IsValid())
        {
            await ApplyProfileAsync(profile.MonitorSettings);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        // Cancel all async operations first
        _cancellationTokenSource?.Cancel();

        // Dispose each resource independently to ensure all get cleaned up
        try
        {
            _displayChangeWatcher?.Dispose();
        }
        catch
        {
        }

        // Dispose monitor view models
        foreach (var vm in Monitors)
        {
            try
            {
                vm.Dispose();
            }
            catch
            {
            }
        }

        try
        {
            _monitorManager?.Dispose();
        }
        catch
        {
        }

        try
        {
            _stateManager?.Dispose();
        }
        catch
        {
        }

        try
        {
            _cancellationTokenSource?.Dispose();
        }
        catch
        {
        }

        try
        {
            Monitors.Clear();
        }
        catch
        {
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Load profiles from disk for quick apply feature
    /// </summary>
    private void LoadProfiles()
    {
        try
        {
            var profilesData = ProfileService.LoadProfiles();
            _profiles.Clear();
            foreach (var profile in profilesData.Profiles)
            {
                _profiles.Add(profile);
            }

            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(ShowProfileSwitcherButton));
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Profile] Failed to load profiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Load UI display settings from settings file
    /// </summary>
    private void LoadUIDisplaySettings()
    {
        try
        {
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            ShowProfileSwitcher = settings.Properties.ShowProfileSwitcher;
            ShowIdentifyMonitorsButton = settings.Properties.ShowIdentifyMonitorsButton;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to load UI display settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles display configuration changes detected by the DisplayChangeWatcher.
    /// The DisplayChangeWatcher already applies the configured delay (MonitorRefreshDelay)
    /// to allow hardware to stabilize, so we can refresh immediately here.
    /// </summary>
    private async void OnDisplayChanged(object? sender, EventArgs e)
    {
        // Set scanning state to provide visual feedback
        IsScanning = true;

        // Perform refresh - DisplayChangeWatcher has already waited for hardware to stabilize
        await RefreshMonitorsAsync(skipScanningCheck: true);
    }

    /// <summary>
    /// Starts watching for display changes. Call after initialization is complete.
    /// </summary>
    public void StartDisplayWatching()
    {
        _displayChangeWatcher.Start();
    }

    /// <summary>
    /// Stops watching for display changes.
    /// </summary>
    public void StopDisplayWatching()
    {
        _displayChangeWatcher.Stop();
    }
}
