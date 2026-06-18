// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
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
using PowerDisplay.Models;
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
public partial class MainViewModel : ObservableObject, IDisposable
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
    private readonly ISystemClock _clock;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMonitors))]
    [NotifyPropertyChangedFor(nameof(ShowNoMonitorsMessage))]
    [NotifyPropertyChangedFor(nameof(IsInteractionEnabled))]
    [NotifyPropertyChangedFor(nameof(IsLinkedBrightnessSliderEnabled))]
    public partial bool IsScanning { get; set; }

    private bool _isInitialized;
    private bool _isLoading;

    [ObservableProperty]
    public partial ObservableCollection<MonitorViewModel> Monitors { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfiles))]
    [NotifyPropertyChangedFor(nameof(ShowProfileSwitcherButton))]
    public partial ObservableCollection<PowerDisplayProfile> Profiles { get; set; }

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
        : this(new SystemClock())
    {
    }

    internal MainViewModel(ISystemClock clock)
    {
        _clock = clock;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _cancellationTokenSource = new CancellationTokenSource();
        Monitors = new ObservableCollection<MonitorViewModel>();
        Profiles = new ObservableCollection<PowerDisplayProfile>();
        IsScanning = true;
        ShowProfileSwitcher = true;
        ShowIdentifyMonitorsButton = true;

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
        _displayChangeWatcher.DisplayChanging += OnDisplayChanging;

        // Start initial discovery
        _ = InitializeAsync(_cancellationTokenSource.Token);
    }

    public bool HasProfiles => Profiles.Count > 0;

    // UI display control properties - loaded from settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProfileSwitcherButton))]
    public partial bool ShowProfileSwitcher { get; set; }

    [ObservableProperty]
    public partial bool ShowIdentifyMonitorsButton { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether brightness slider changes are broadcast to all
    /// non-excluded monitors as one linked level. Persisted in <c>PowerDisplaySettings</c> so
    /// the choice survives restarts. The toggle is meaningful only when two or more monitors
    /// are connected — see <see cref="ShowLinkLevelsToggle"/> for the visibility gate.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIndividualDisplays))]
    [NotifyPropertyChangedFor(nameof(ShowLinkLevelsToggle))]
    [NotifyPropertyChangedFor(nameof(ShowLinkLevelsInactiveIcon))]
    public partial bool LinkedLevelsActive { get; set; }

    /// <summary>
    /// Gets or sets the brightness value driving the linked "All Displays" slider. Setter is
    /// debounced and broadcasts on commit to every linked target: a
    /// <see cref="MonitorViewModel.SupportsBrightness"/> monitor not excluded from sync (see
    /// <c>OnLinkedBrightnessChanged</c>). Synchronously updates each linked monitor's brightness
    /// value so it is correct if the monitor is later excluded or link mode is disabled — the
    /// linked broadcast is the single source of hardware writes while link mode is active.
    /// </summary>
    [ObservableProperty]
    public partial int LinkedBrightness { get; set; }

    /// <summary>
    /// Gets a value indicating whether the linked brightness slider has at least one linked
    /// brightness-capable monitor to control.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinkedBrightnessSliderEnabled))]
    public partial bool IsLinkedBrightnessAvailable { get; set; }

    /// <summary>
    /// Gets a value indicating whether the linked "All Displays" brightness slider accepts input.
    /// Disabled both when no linked target is available and while monitor discovery/restore is busy.
    /// </summary>
    public bool IsLinkedBrightnessSliderEnabled => IsLinkedBrightnessAvailable && IsInteractionEnabled;

    /// <summary>
    /// Gets the count of monitors that participate in linked brightness — used for the
    /// "All Displays" card subtitle ("N linked"). Counts brightness-capable monitors the user
    /// has not excluded (see <see cref="MonitorViewModel.IsExcludedFromSync"/>).
    /// </summary>
    public int LinkedMonitorsCount => Monitors.Count(m => m.SupportsBrightness && !IsMonitorExcludedFromSync(m.Id));

    /// <summary>
    /// Gets the count of brightness-capable monitors excluded from linked brightness.
    /// </summary>
    public int ExcludedMonitorsCount => Monitors.Count(m => m.SupportsBrightness && IsMonitorExcludedFromSync(m.Id));

    /// <summary>
    /// Gets a value indicating whether the "N excluded" subtitle fragment should be visible on
    /// the "All displays" card.
    /// </summary>
    public bool HasExcludedMonitors => ExcludedMonitorsCount > 0;

    /// <summary>
    /// Gets the localized "N linked" subtitle shown on the "All Displays" card. Recomputed
    /// alongside <see cref="LinkedMonitorsCount"/> via <c>RecomputeLinkedBrightnessAvailability</c>.
    /// </summary>
    public string LinkedMonitorsCountText =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Helpers.ResourceLoaderInstance.ResourceLoader.GetString("AllDisplaysLinkedCountFormat"),
            LinkedMonitorsCount);

    /// <summary>
    /// Gets the localized "N excluded" subtitle fragment shown on the "All Displays" card.
    /// </summary>
    public string ExcludedMonitorsCountText =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Helpers.ResourceLoaderInstance.ResourceLoader.GetString("AllDisplaysExcludedCountFormat"),
            ExcludedMonitorsCount);

    /// <summary>
    /// Gets or sets a value indicating whether the "Individual displays" section is expanded
    /// while link mode is on. Collapsed by default so linked mode reads as a single master
    /// slider; expanding reveals the per-monitor cards (with linked monitors' sliders disabled).
    /// Ignored when link mode is off — see <see cref="ShowIndividualDisplays"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIndividualDisplays))]
    [NotifyPropertyChangedFor(nameof(ShowIndividualDisplaysCollapsedIcon))]
    public partial bool IndividualDisplaysExpanded { get; set; }

    /// <summary>
    /// Gets a value indicating whether the per-monitor cards list is shown. Always shown when
    /// link mode is off (the classic layout); when link mode is on the cards are tucked into the
    /// collapsible "Individual displays" section and only shown once the user expands it.
    /// </summary>
    public bool ShowIndividualDisplays => !LinkedLevelsActive || IndividualDisplaysExpanded;

    public bool ShowIndividualDisplaysCollapsedIcon => !IndividualDisplaysExpanded;

    /// <summary>
    /// Gets a value indicating whether the link-levels toggle should be visible. Shown when at
    /// least two monitors report <see cref="MonitorViewModel.SupportsBrightness"/> (the entry-point
    /// gate — counting raw entries would show the toggle even when only one display can actually be
    /// driven), OR whenever link mode is already active. The second clause guarantees the user can
    /// always turn link mode back off, even if monitors were unplugged down to one controllable
    /// display while linked — otherwise the "All displays" card would strand them with no way out.
    /// Recomputed by <see cref="UpdateMonitorList"/> and on <see cref="LinkedLevelsActive"/> change.
    /// </summary>
    public bool ShowLinkLevelsToggle => LinkedLevelsActive || Monitors.Count(m => m.SupportsBrightness) >= 2;

    public bool ShowLinkLevelsInactiveIcon => !LinkedLevelsActive;

    /// <summary>
    /// Gets a value indicating whether to show the profile switcher button.
    /// Combines settings value with HasProfiles check.
    /// </summary>
    public bool ShowProfileSwitcherButton => ShowProfileSwitcher && HasProfiles;

    // Custom VCP mappings - loaded from settings
    private List<CustomVcpValueMapping> _customVcpMappings = new();

    /// <summary>
    /// Gets or sets the custom VCP value name mappings.
    /// These mappings override the default VCP value names for color temperature and input source.
    /// </summary>
    public List<CustomVcpValueMapping> CustomVcpMappings
    {
        get => _customVcpMappings;
        set
        {
            _customVcpMappings = value ?? new List<CustomVcpValueMapping>();
            OnPropertyChanged();
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
            OnPropertyChanged(nameof(IsLinkedBrightnessSliderEnabled));
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
            var allDisplayInfo = DisplayConfigInventory.GetAllMonitorDisplayInfo().Values.ToList();

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

                // Create and position identify window.
                // Position before Activate so the window appears directly at the target
                // location — avoiding a visible flicker from the default spawn position
                // and skipping a WM_DPICHANGED round-trip when crossing DPI monitors.
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

    public void Dispose()
    {
        // Cancel all async operations first
        _cancellationTokenSource?.Cancel();

        // Stop the linked-brightness debounce timer so its Tick handler does not fire after
        // we have already cleared Monitors below (broadcast would iterate an empty list).
        try
        {
            _linkedBrightnessCommitTimer?.Stop();
        }
        catch
        {
        }

        // Dispose each resource independently to ensure all get cleaned up
        try
        {
            if (_displayChangeWatcher is not null)
            {
                _displayChangeWatcher.DisplayChanging -= OnDisplayChanging;
                _displayChangeWatcher.DisplayChanged -= OnDisplayChanged;
                _displayChangeWatcher.Dispose();
            }
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
            Profiles.Clear();
            foreach (var profile in profilesData.Profiles)
            {
                Profiles.Add(profile);
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

            // Load the linked-brightness exclusion set before applying LinkedLevelsActive. If this
            // method runs after monitors are already discovered, the toggle hook can seed the master
            // slider immediately and must see the persisted exclusions.
            LoadExcludedMonitorIds(settings.Properties.ExcludedFromSyncMonitorIds);

            LinkedLevelsActive = settings.Properties.LinkedLevelsActive;

            // Load custom VCP mappings (now using shared type from PowerDisplay.Common.Models)
            CustomVcpMappings = settings.Properties.CustomVcpMappings?.ToList() ?? new List<CustomVcpValueMapping>();
            Logger.LogInfo($"[Settings] Loaded {CustomVcpMappings.Count} custom VCP mappings");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to load UI display settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Invoked synchronously as soon as a display configuration change is
    /// detected (device added/removed, or wake from sleep), before the debounce
    /// delay elapses. Locks the interactive UI by setting IsScanning = true so
    /// the user cannot operate on monitors that are about to disappear or be
    /// re-enumerated by the rediscovery pass <see cref="OnDisplayChanged"/>
    /// will run once debounce completes.
    /// </summary>
    private void OnDisplayChanging(object? sender, EventArgs e)
    {
        CancelPendingLinkedBrightnessCommit();

        if (!IsScanning)
        {
            Logger.LogInfo("[MainViewModel] Display change detected — locking UI ahead of rediscovery");
            IsScanning = true;
        }
    }

    /// <summary>
    /// Handles display configuration changes once the DisplayChangeWatcher's
    /// debounce delay has elapsed. IsScanning was already set by
    /// <see cref="OnDisplayChanging"/> when the change was first detected, so
    /// we just run discovery here.
    /// </summary>
    private async void OnDisplayChanged(object? sender, EventArgs e)
    {
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
