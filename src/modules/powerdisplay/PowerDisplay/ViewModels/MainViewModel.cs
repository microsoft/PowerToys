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
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
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

    private readonly MonitorManager _monitorManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ISettingsUtils _settingsUtils;
    private readonly MonitorStateManager _stateManager;
    private readonly LightSwitchListener _lightSwitchListener;
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

        // Initialize and start LightSwitch integration listener
        _lightSwitchListener = new LightSwitchListener();
        _lightSwitchListener.ThemeChanged += OnLightSwitchThemeChanged;
        _lightSwitchListener.Start();

        // Load profiles for quick apply feature
        LoadProfiles();

        // Initialize display change watcher for auto-refresh on monitor plug/unplug
        _displayChangeWatcher = new DisplayChangeWatcher(_dispatcherQueue);
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
    /// Gets a value indicating whether gets whether user interaction is enabled (not loading or scanning)
    /// </summary>
    public bool IsInteractionEnabled => !IsLoading && !IsScanning;

    [RelayCommand]
    private async Task RefreshAsync() => await RefreshMonitorsAsync();

    [RelayCommand]
    private unsafe void IdentifyMonitors()
    {
        Logger.LogInfo("Identify monitors feature triggered");

        try
        {
            // Get all display areas
            var displayAreas = DisplayArea.FindAll();
            Logger.LogDebug($"Found {displayAreas.Count} display areas");

            // Get GDI device name to MonitorNumber mapping from QueryDisplayConfig
            var displayInfoByGdiName = DdcCiNative.GetAllMonitorDisplayInfo()
                .Values
                .Where(info => info.MonitorNumber > 0)
                .ToDictionary(info => info.GdiDeviceName, info => info.MonitorNumber, StringComparer.OrdinalIgnoreCase);
            Logger.LogDebug($"Found {displayInfoByGdiName.Count} monitors with valid MonitorNumber from QueryDisplayConfig");

            // For each DisplayArea, get its HMONITOR, then get GDI device name to find MonitorNumber
            int windowsCreated = 0;
            for (int i = 0; i < displayAreas.Count; i++)
            {
                var displayArea = displayAreas[i];

                // Convert DisplayId to HMONITOR
                var hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
                if (hMonitor == IntPtr.Zero)
                {
                    Logger.LogDebug($"DisplayArea[{i}]: Failed to get HMONITOR");
                    continue;
                }

                // Get GDI device name from HMONITOR
                var monitorInfo = new MonitorInfoEx { CbSize = (uint)sizeof(MonitorInfoEx) };
                if (!GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    Logger.LogDebug($"DisplayArea[{i}]: GetMonitorInfo failed");
                    continue;
                }

                var gdiDeviceName = monitorInfo.GetDeviceName();

                // Look up MonitorNumber by GDI device name
                if (!displayInfoByGdiName.TryGetValue(gdiDeviceName, out int monitorNumber))
                {
                    Logger.LogDebug($"DisplayArea[{i}]: No MonitorNumber found for GDI device '{gdiDeviceName}'");
                    continue;
                }

                Logger.LogDebug($"DisplayArea[{i}]: GDI='{gdiDeviceName}' -> MonitorNumber={monitorNumber}");

                // Create and position identify window
                var identifyWindow = new IdentifyWindow(monitorNumber);
                identifyWindow.PositionOnDisplay(displayArea);
                identifyWindow.Activate();
                windowsCreated++;
            }

            Logger.LogInfo($"Created {windowsCreated} identify windows");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to identify monitors: {ex.Message}");
            Logger.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task ApplyProfile(PowerDisplayProfile? profile)
    {
        if (profile != null && profile.IsValid())
        {
            Logger.LogInfo($"[Profile] Applying profile '{profile.Name}' from quick apply");
            await ApplyProfileAsync(profile.Name, profile.MonitorSettings);
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

        // Dispose all resources safely (don't throw from Dispose)
        SafeDispose(_displayChangeWatcher, "DisplayChangeWatcher");
        SafeDispose(_lightSwitchListener, "LightSwitchListener");

        // Dispose monitor view models
        foreach (var vm in Monitors)
        {
            SafeDispose(vm, "MonitorViewModel");
        }

        SafeExecute(() => Monitors.Clear(), "Monitors.Clear");
        SafeDispose(_monitorManager, "MonitorManager");
        SafeDispose(_stateManager, "StateManager");
        SafeDispose(_cancellationTokenSource, "CancellationTokenSource");

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Safely dispose an object without throwing exceptions
    /// </summary>
    private static void SafeDispose(IDisposable? disposable, string name)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Error disposing {name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely execute an action without throwing exceptions
    /// </summary>
    private static void SafeExecute(Action action, string name)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Error executing {name}: {ex.Message}");
        }
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
            Logger.LogInfo($"[Profile] Loaded {_profiles.Count} profiles for quick apply");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Profile] Failed to load profiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles display configuration changes detected by the DisplayChangeWatcher.
    /// Triggers a monitor refresh to update the UI.
    /// </summary>
    private async void OnDisplayChanged(object? sender, EventArgs e)
    {
        Logger.LogInfo("[MainViewModel] Display change detected, refreshing monitors...");
        await RefreshMonitorsAsync();
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
