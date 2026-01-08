// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;

namespace PowerDisplay.Helpers;

/// <summary>
/// Watches for display/monitor connection changes using WinRT DeviceWatcher.
/// Triggers DisplayChanged event when monitors are added, removed, or updated.
/// </summary>
public sealed partial class DisplayChangeWatcher : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly TimeSpan _debounceDelay;

    private DeviceWatcher? _deviceWatcher;
    private CancellationTokenSource? _debounceCts;
    private bool _isRunning;
    private bool _disposed;
    private bool _initialEnumerationComplete;

    /// <summary>
    /// Event triggered when display configuration changes (after debounce period).
    /// </summary>
    public event EventHandler? DisplayChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayChangeWatcher"/> class.
    /// </summary>
    /// <param name="dispatcherQueue">The dispatcher queue for UI thread marshalling.</param>
    /// <param name="debounceDelay">Delay before triggering DisplayChanged event. This allows hardware to stabilize after monitor plug/unplug.</param>
    public DisplayChangeWatcher(DispatcherQueue dispatcherQueue, TimeSpan debounceDelay)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _debounceDelay = debounceDelay;
    }

    /// <summary>
    /// Gets a value indicating whether the watcher is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Starts watching for display changes.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
        {
            return;
        }

        try
        {
            // Get the device selector for display monitors
            string selector = DisplayMonitor.GetDeviceSelector();

            // Create the device watcher
            _deviceWatcher = DeviceInformation.CreateWatcher(selector);

            // Subscribe to events
            _deviceWatcher.Added += OnDeviceAdded;
            _deviceWatcher.Removed += OnDeviceRemoved;
            _deviceWatcher.Updated += OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
            _deviceWatcher.Stopped += OnWatcherStopped;

            // Reset state before starting (must be before Start() to avoid race)
            _initialEnumerationComplete = false;
            _isRunning = true;

            // Start watching
            _deviceWatcher.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Failed to start: {ex.Message}");
            _isRunning = false;
        }
    }

    /// <summary>
    /// Stops watching for display changes.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning || _deviceWatcher == null)
        {
            return;
        }

        try
        {
            // Cancel any pending debounce
            CancelDebounce();

            // Stop the watcher
            _deviceWatcher.Stop();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Error stopping watcher: {ex.Message}");
        }
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
    {
        // Dispatch to UI thread to ensure thread-safe state access
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Ignore events during initial enumeration or after disposal
            if (_disposed || !_initialEnumerationComplete)
            {
                return;
            }

            ScheduleDisplayChanged();
        });
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        // Dispatch to UI thread to ensure thread-safe state access
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Ignore events during initial enumeration or after disposal
            if (_disposed || !_initialEnumerationComplete)
            {
                return;
            }

            ScheduleDisplayChanged();
        });
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        // Only trigger refresh for significant updates, not every property change.
        // For now, we'll skip updates to avoid excessive refreshes.
        // The Added and Removed events are the primary triggers for monitor changes.
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        // Dispatch to UI thread to ensure thread-safe state access
        _dispatcherQueue.TryEnqueue(() =>
        {
            _initialEnumerationComplete = true;
        });
    }

    private void OnWatcherStopped(DeviceWatcher sender, object args)
    {
        // Dispatch to UI thread to ensure thread-safe state access
        _dispatcherQueue.TryEnqueue(() =>
        {
            _isRunning = false;
            _initialEnumerationComplete = false;
        });
    }

    /// <summary>
    /// Schedules a DisplayChanged event with debouncing.
    /// Multiple rapid changes will only trigger one event after the debounce period.
    /// </summary>
    private void ScheduleDisplayChanged()
    {
        // Cancel any pending debounce
        CancelDebounce();

        // Create new cancellation token
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        // Schedule the event after debounce delay
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceDelay, token);

                if (!token.IsCancellationRequested)
                {
                    // Dispatch to UI thread
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (!_disposed)
                        {
                            DisplayChanged?.Invoke(this, EventArgs.Empty);
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Debounce was cancelled by a newer event, this is expected
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DisplayChangeWatcher] Error in debounce task: {ex.Message}");
            }
        });
    }

    private void CancelDebounce()
    {
        try
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
    }

    /// <summary>
    /// Disposes resources used by the watcher.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop watching
        Stop();

        // Unsubscribe from events
        if (_deviceWatcher != null)
        {
            _deviceWatcher.Added -= OnDeviceAdded;
            _deviceWatcher.Removed -= OnDeviceRemoved;
            _deviceWatcher.Updated -= OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
            _deviceWatcher.Stopped -= OnWatcherStopped;
            _deviceWatcher = null;
        }

        // Cancel debounce
        CancelDebounce();
    }
}
