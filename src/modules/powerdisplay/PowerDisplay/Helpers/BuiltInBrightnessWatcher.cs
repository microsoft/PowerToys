// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerDisplay.Helpers;

/// <summary>
/// Raises <see cref="BrightnessChanged"/> when Windows changes the built-in display
/// brightness setting (<see cref="PowerSettingsNative.GuidDevicePowerPolicyVideoBrightness"/>).
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
    private readonly PowerSettingSubscription _subscription;

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

    /// <summary>
    /// Gets the last brightness percentage Windows reported, or -1 before the initial notification.
    /// </summary>
    public int LastBrightness => Volatile.Read(ref _lastBrightness);

    public BuiltInBrightnessWatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _subscription = new PowerSettingSubscription(
            PowerSettingsNative.GuidDevicePowerPolicyVideoBrightness,
            "GUID_DEVICE_POWER_POLICY_VIDEO_BRIGHTNESS",
            nameof(BuiltInBrightnessWatcher),
            OnSettingChangedNative);
    }

    /// <summary>
    /// Invoked by Windows on an internal worker thread. Reads the DWORD payload and
    /// marshals it to the dispatcher thread.
    /// </summary>
    private void OnSettingChangedNative(IntPtr setting)
    {
        int value;
        try
        {
            value = Marshal.ReadInt32(setting, PowerSettingsNative.PowerBroadcastSettingDataOffset);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[BuiltInBrightnessWatcher] Failed to read POWERBROADCAST_SETTING data: {ex.Message}");
            return;
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
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscription.Dispose();
    }
}
