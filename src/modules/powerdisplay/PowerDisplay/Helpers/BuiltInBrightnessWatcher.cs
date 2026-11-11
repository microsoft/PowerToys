// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerDisplay.Helpers;

/// <summary>
/// Raises <see cref="BrightnessChanged"/> when Windows changes the built-in display
/// brightness setting (<see cref="PowerSettingsNative.GuidVideoCurrentMonitorBrightness"/>).
/// </summary>
/// <remarks>
/// Laptop brightness keys are usually HID consumer-control usages (0x6F/0x70) that
/// Windows handles itself: they produce no virtual-key code, so keyboard hooks and
/// Keyboard Manager never see them. Windows still updates this power setting on every
/// press, even when the built-in panel is turned off, so observing it is the reliable
/// way to let those keys drive external monitors.
/// </remarks>
internal sealed partial class BuiltInBrightnessWatcher : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;

    private IntPtr _registration;
    private PowerSettingsNative.DeviceNotifyCallbackRoutine? _callback;
    private GCHandle _callbackHandle;

    // Last value seen; -1 until Windows' initial echo on subscription arrives. The echo
    // only seeds this field, so enabling the watcher never writes hardware by itself.
    // Mutated only on the dispatcher thread.
    private int _lastBrightness = -1;
    private bool _disposed;

    /// <summary>
    /// Raised on the dispatcher thread with the new brightness percentage (0-100)
    /// each time the setting actually changes.
    /// </summary>
    public event EventHandler<int>? BrightnessChanged;

    public BuiltInBrightnessWatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        Register();
    }

    private void Register()
    {
        _callback = OnSettingChangedNative;

        // GetFunctionPointerForDelegate does not root the delegate; keep it alive
        // for the whole registration lifetime.
        _callbackHandle = GCHandle.Alloc(_callback);

        var subscribeParams = new PowerSettingsNative.DeviceNotifySubscribeParameters
        {
            Callback = Marshal.GetFunctionPointerForDelegate(_callback),
            Context = IntPtr.Zero,
        };

        var guid = PowerSettingsNative.GuidVideoCurrentMonitorBrightness;
        uint result = PowerSettingsNative.PowerSettingRegisterNotification(
            ref guid,
            PowerSettingsNative.DeviceNotifyCallback,
            ref subscribeParams,
            out _registration);

        if (result != 0)
        {
            Logger.LogWarning($"[BuiltInBrightnessWatcher] PowerSettingRegisterNotification failed: 0x{result:X}");
            _registration = IntPtr.Zero;
        }
        else
        {
            Logger.LogInfo("[BuiltInBrightnessWatcher] Subscribed to GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS");
        }
    }

    /// <summary>
    /// Invoked by Windows on an internal worker thread. Reads the DWORD payload and
    /// marshals it to the dispatcher thread.
    /// </summary>
    private uint OnSettingChangedNative(IntPtr context, uint type, IntPtr setting)
    {
        if (type != PowerSettingsNative.PowerSettingChangeNotification || setting == IntPtr.Zero)
        {
            return 0;
        }

        int value;
        try
        {
            value = Marshal.ReadInt32(setting, PowerSettingsNative.PowerBroadcastSettingDataOffset);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[BuiltInBrightnessWatcher] Failed to read POWERBROADCAST_SETTING data: {ex.Message}");
            return 0;
        }

        value = Math.Clamp(value, 0, 100);

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            int previous = _lastBrightness;
            _lastBrightness = value;

            if (previous < 0 || previous == value)
            {
                return;
            }

            BrightnessChanged?.Invoke(this, value);
        });

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_registration != IntPtr.Zero)
        {
            try
            {
                uint result = PowerSettingsNative.PowerSettingUnregisterNotification(_registration);
                if (result != 0)
                {
                    Logger.LogWarning($"[BuiltInBrightnessWatcher] PowerSettingUnregisterNotification failed: 0x{result:X}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BuiltInBrightnessWatcher] Unregister failed: {ex.Message}");
            }

            _registration = IntPtr.Zero;
        }

        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }
    }
}
