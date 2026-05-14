// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerDisplay.Common.Helpers;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;

namespace PowerDisplay.Helpers;

/// <summary>
/// Watches for display/monitor configuration changes and emits coalesced refresh
/// signals. Two independent sources feed in:
///
/// 1. <see cref="DeviceWatcher"/> over <c>DisplayMonitor</c> — picks up device
///    enumeration changes (plug, unplug, GPU rebind).
/// 2. <see cref="PowerSettingsNative.GuidConsoleDisplayState"/> — fires whenever
///    the console display transitions on/off/dimmed. Reliable on both S3 and
///    S0ix and the only signal that catches idle-blank or lid recovery.
///
/// On display turning ON, the watcher first fires <see cref="ResumeDetected"/>
/// synchronously (so the ViewModel can lock interactive UI immediately), then
/// schedules a debounced <see cref="DisplayChanged"/> so callers re-discover
/// hardware once it has stabilised.
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

    // Console-display-state subscription
    private IntPtr _displayStateRegistration;
    private PowerSettingsNative.DeviceNotifyCallbackRoutine? _displayStateCallback;
    private GCHandle _displayStateCallbackHandle;

    // Seeded to ON so Windows' initial-state echo on subscription does not
    // trigger a spurious rescan. Mutated only on the dispatcher thread.
    private uint _lastDisplayState = PowerSettingsNative.DisplayStateOn;

    /// <summary>
    /// Event triggered when display configuration changes (after debounce period).
    /// Subscribers (typically the ViewModel) should re-run monitor discovery.
    /// </summary>
    public event EventHandler? DisplayChanged;

    /// <summary>
    /// Event triggered the moment we detect the console display transitioning
    /// to ON. Fires on the dispatcher thread, synchronously, BEFORE the debounce
    /// delay elapses — gives subscribers a chance to lock interactive UI so
    /// users can't act on now-stale DDC/CI handles during the rediscovery
    /// window.
    /// </summary>
    public event EventHandler? ResumeDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayChangeWatcher"/> class.
    /// </summary>
    /// <param name="dispatcherQueue">The dispatcher queue for UI thread marshalling.</param>
    /// <param name="debounceDelay">Delay before triggering DisplayChanged event. Allows hardware to stabilize after plug/unplug or wake.</param>
    public DisplayChangeWatcher(DispatcherQueue dispatcherQueue, TimeSpan debounceDelay)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _debounceDelay = debounceDelay;

        RegisterDisplayStateNotification();
    }

    public bool IsRunning => _isRunning;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
        {
            return;
        }

        try
        {
            string selector = DisplayMonitor.GetDeviceSelector();

            _deviceWatcher = DeviceInformation.CreateWatcher(selector);
            _deviceWatcher.Added += OnDeviceAdded;
            _deviceWatcher.Removed += OnDeviceRemoved;
            _deviceWatcher.Updated += OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
            _deviceWatcher.Stopped += OnWatcherStopped;

            _initialEnumerationComplete = false;
            _isRunning = true;

            _deviceWatcher.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Failed to start: {ex.Message}");
            _isRunning = false;
        }
    }

    public void Stop()
    {
        if (!_isRunning || _deviceWatcher == null)
        {
            return;
        }

        try
        {
            CancelDebounce();
            _deviceWatcher.Stop();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Error stopping watcher: {ex.Message}");
        }
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || !_initialEnumerationComplete)
            {
                return;
            }

            ScheduleDisplayChanged();
        });
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || !_initialEnumerationComplete)
            {
                return;
            }

            ScheduleDisplayChanged();
        });
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        // Property-level updates fire frequently and are not actionable here.
        // Added/Removed are the primary device-level triggers.
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _initialEnumerationComplete = true;
        });
    }

    private void OnWatcherStopped(DeviceWatcher sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _isRunning = false;

            if (!_disposed)
            {
                Logger.LogInfo("[DisplayChangeWatcher] Watcher stopped unexpectedly, attempting restart");
                CleanupDeviceWatcher();

                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (!_disposed && !_isRunning)
                        {
                            Start();
                        }
                    });
                });
            }
            else
            {
                _initialEnumerationComplete = false;
            }
        });
    }

    private void RegisterDisplayStateNotification()
    {
        _displayStateCallback = OnDisplayStateChangedNative;
        _displayStateCallbackHandle = GCHandle.Alloc(_displayStateCallback);

        var subscribeParams = new PowerSettingsNative.DeviceNotifySubscribeParameters
        {
            Callback = Marshal.GetFunctionPointerForDelegate(_displayStateCallback),
            Context = IntPtr.Zero,
        };

        var guid = PowerSettingsNative.GuidConsoleDisplayState;
        uint result = PowerSettingsNative.PowerSettingRegisterNotification(
            ref guid,
            PowerSettingsNative.DeviceNotifyCallback,
            ref subscribeParams,
            out _displayStateRegistration);

        if (result != 0)
        {
            Logger.LogWarning(
                $"[DisplayChangeWatcher] PowerSettingRegisterNotification failed: 0x{result:X}");
            _displayStateRegistration = IntPtr.Zero;
        }
        else
        {
            Logger.LogInfo("[DisplayChangeWatcher] Subscribed to GUID_CONSOLE_DISPLAY_STATE");
        }
    }

    /// <summary>
    /// Callback invoked by Windows on an internal worker thread when the
    /// console display power state changes. Reads the new state, marshals to
    /// the dispatcher, and lets <see cref="HandleDisplayStateChange"/> apply
    /// the policy.
    /// </summary>
    private uint OnDisplayStateChangedNative(IntPtr context, uint type, IntPtr setting)
    {
        if (type != PowerSettingsNative.PbtPowerSettingChange || setting == IntPtr.Zero)
        {
            return 0;
        }

        uint newState;
        try
        {
            newState = Marshal.ReadByte(setting, PowerSettingsNative.PowerBroadcastSettingDataOffset);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Failed to read POWERBROADCAST_SETTING data: {ex.Message}");
            return 0;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            HandleDisplayStateChange(newState);
        });

        return 0;
    }

    /// <summary>
    /// Runs on the dispatcher thread. Compares the new state against the last
    /// seen state, fires ResumeDetected synchronously if appropriate, and
    /// schedules the debounced DisplayChanged.
    /// </summary>
    private void HandleDisplayStateChange(uint newState)
    {
        uint lastState = _lastDisplayState;
        _lastDisplayState = newState;

        // Wake = transition INTO ON from any other state. ON→ON is a
        // subscription echo, ON→OFF / ON→DIMMED are blanking (no rescan needed).
        bool isWakeTransition = newState == PowerSettingsNative.DisplayStateOn
                                && lastState != PowerSettingsNative.DisplayStateOn;
        if (!isWakeTransition)
        {
            return;
        }

        Logger.LogInfo(
            $"[DisplayChangeWatcher] Console display ON (was {DescribeState(lastState)}); " +
            $"firing ResumeDetected and scheduling rescan");

        // Synchronously notify subscribers that a wake transition has happened
        // BEFORE we start the debounce — gives the ViewModel a chance to lock
        // interactive UI for the entire rediscovery window.
        ResumeDetected?.Invoke(this, EventArgs.Empty);

        ScheduleDisplayChanged();
    }

    private static string DescribeState(uint state) => state switch
    {
        PowerSettingsNative.DisplayStateOff => "off",
        PowerSettingsNative.DisplayStateOn => "on",
        PowerSettingsNative.DisplayStateDimmed => "dimmed",
        _ => $"unknown(0x{state:X})",
    };

    private void CleanupDeviceWatcher()
    {
        if (_deviceWatcher != null)
        {
            try
            {
                _deviceWatcher.Added -= OnDeviceAdded;
                _deviceWatcher.Removed -= OnDeviceRemoved;
                _deviceWatcher.Updated -= OnDeviceUpdated;
                _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
                _deviceWatcher.Stopped -= OnWatcherStopped;
            }
            catch
            {
                // Ignore cleanup errors.
            }

            _deviceWatcher = null;
        }
    }

    private void ScheduleDisplayChanged()
    {
        CancelDebounce();

        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceDelay, token);

                if (!token.IsCancellationRequested)
                {
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
                // Expected — a newer event superseded this one.
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
            // Already disposed.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_displayStateRegistration != IntPtr.Zero)
        {
            try
            {
                uint unregisterResult = PowerSettingsNative.PowerSettingUnregisterNotification(_displayStateRegistration);
                if (unregisterResult != 0)
                {
                    Logger.LogWarning(
                        $"[DisplayChangeWatcher] PowerSettingUnregisterNotification failed: 0x{unregisterResult:X}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DisplayChangeWatcher] Unregister failed: {ex.Message}");
            }

            _displayStateRegistration = IntPtr.Zero;
        }

        if (_displayStateCallbackHandle.IsAllocated)
        {
            _displayStateCallbackHandle.Free();
        }

        Stop();
        CleanupDeviceWatcher();
        CancelDebounce();
    }
}
