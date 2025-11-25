// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using PowerDisplay.Commands;
using PowerDisplay.Common.Services;
using PowerDisplay.Core;
using PowerDisplay.Helpers;

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
    private readonly MonitorManager _monitorManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ISettingsUtils _settingsUtils;
    private readonly MonitorStateManager _stateManager;
    private readonly LightSwitchListener _lightSwitchListener;

    private ObservableCollection<MonitorViewModel> _monitors;
    private string _statusText;
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
        _statusText = "Initializing...";
        _isScanning = true;

        // Initialize settings utils
        _settingsUtils = new SettingsUtils();
        _stateManager = new MonitorStateManager();

        // Initialize the monitor manager
        _monitorManager = new MonitorManager();

        // Subscribe to events
        _monitorManager.MonitorsChanged += OnMonitorsChanged;

        // Initialize and start LightSwitch integration listener
        _lightSwitchListener = new LightSwitchListener();
        _lightSwitchListener.ThemeChanged += OnLightSwitchThemeChanged;
        _lightSwitchListener.Start();

        // Start initial discovery
        _ = InitializeAsync();
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

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
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
    /// Gets a value indicating whether gets whether user interaction is enabled (not loading or scanning)
    /// </summary>
    public bool IsInteractionEnabled => !IsLoading && !IsScanning;

    public ICommand RefreshCommand => new RelayCommand(async () => await RefreshMonitorsAsync());

    public ICommand SetAllBrightnessCommand => new RelayCommand<int?>(async (brightness) =>
    {
        if (brightness.HasValue)
        {
            await SetAllBrightnessAsync(brightness.Value);
        }
    });

    public ICommand IdentifyMonitorsCommand => new RelayCommand(() =>
    {
        // For now, just show a simple notification that the feature is implemented
        // Full implementation would require creating WinUI 3 windows positioned on each monitor
        // which is more complex than WPF due to different windowing model
        Logger.LogInfo("Identify monitors feature triggered");

        // TODO: Implement proper WinUI 3 window positioning for each monitor
        // This requires:
        // 1. Creating a separate Window for each monitor
        // 2. Using AppWindow.MoveAndResize with monitor coordinates
        // 3. Handling DPI scaling correctly
    });

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
}
