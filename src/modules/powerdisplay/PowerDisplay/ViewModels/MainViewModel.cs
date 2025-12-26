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
            // Get all display areas (virtual desktop regions)
            var displayAreas = DisplayArea.FindAll();

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

            // For each DisplayArea, get its HMONITOR, then get GDI device name to find MonitorNumber(s)
            int windowsCreated = 0;
            for (int i = 0; i < displayAreas.Count; i++)
            {
                var displayArea = displayAreas[i];

                // Convert DisplayId to HMONITOR
                var hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
                if (hMonitor == IntPtr.Zero)
                {
                    continue;
                }

                // Get GDI device name from HMONITOR
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

                // Create and position identify window
                var identifyWindow = new IdentifyWindow(displayText);
                identifyWindow.PositionOnDisplay(displayArea);
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
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Profile] Failed to load profiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles display configuration changes detected by the DisplayChangeWatcher.
    /// Triggers a monitor refresh to update the UI after a delay to allow hardware to stabilize.
    /// </summary>
    private async void OnDisplayChanged(object? sender, EventArgs e)
    {
        // Get the delay from settings (default to 5 seconds if not configured)
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        int delaySeconds = settings?.Properties?.MonitorRefreshDelay ?? 5;

        // Clamp to reasonable range (1-30 seconds)
        delaySeconds = Math.Clamp(delaySeconds, 1, 30);

        // Set scanning state immediately to provide visual feedback
        IsScanning = true;

        // Wait for hardware to stabilize (DDC/CI may not be ready immediately after plug)
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

        // Perform actual refresh - skip scanning check since we already set IsScanning above
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
