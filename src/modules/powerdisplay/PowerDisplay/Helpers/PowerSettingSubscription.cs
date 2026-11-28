// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace PowerDisplay.Helpers;

/// <summary>
/// Owns one <see cref="PowerSettingsNative.PowerSettingRegisterNotification"/> registration:
/// keeps the native callback delegate alive for the registration lifetime and unregisters on
/// disposal. Payload decoding, initial-notification handling and event policy stay with the
/// owning watcher.
/// </summary>
/// <remarks>
/// <c>partial</c> because <see cref="IDisposable"/> is projected to WinRT (CsWinRT1028).
/// </remarks>
internal sealed partial class PowerSettingSubscription : IDisposable
{
    private readonly string _logTag;
    private readonly Action<IntPtr> _onSettingChanged;

    // GetFunctionPointerForDelegate does not root the delegate; the GCHandle keeps it alive
    // until Dispose so Windows never invokes a collected delegate.
    private readonly PowerSettingsNative.DeviceNotifyCallbackRoutine _callback;
    private GCHandle _callbackHandle;
    private IntPtr _registration;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerSettingSubscription"/> class and registers
    /// for <paramref name="settingGuid"/>. A failed registration is logged and leaves the instance
    /// inert, matching the watchers' best-effort behavior.
    /// </summary>
    /// <param name="settingGuid">Power setting to subscribe to.</param>
    /// <param name="settingName">SDK name of the setting, used in log messages.</param>
    /// <param name="logTag">Owner name used as the log prefix.</param>
    /// <param name="onSettingChanged">
    /// Invoked on a Windows worker thread with a pointer to the POWERBROADCAST_SETTING for each
    /// setting-change notification (including the initial one Windows sends on subscription).
    /// The pointer is only valid during the call; the handler must copy what it needs and marshal
    /// to its own thread.
    /// </param>
    public PowerSettingSubscription(Guid settingGuid, string settingName, string logTag, Action<IntPtr> onSettingChanged)
    {
        _logTag = logTag;
        _onSettingChanged = onSettingChanged ?? throw new ArgumentNullException(nameof(onSettingChanged));

        _callback = OnSettingChangedNative;
        _callbackHandle = GCHandle.Alloc(_callback);

        var subscribeParams = new PowerSettingsNative.DeviceNotifySubscribeParameters
        {
            Callback = Marshal.GetFunctionPointerForDelegate(_callback),
            Context = IntPtr.Zero,
        };

        uint result = PowerSettingsNative.PowerSettingRegisterNotification(
            ref settingGuid,
            PowerSettingsNative.DeviceNotifyCallback,
            ref subscribeParams,
            out _registration);

        if (result != 0)
        {
            Logger.LogWarning($"[{_logTag}] PowerSettingRegisterNotification failed: 0x{result:X}");
            _registration = IntPtr.Zero;
        }
        else
        {
            Logger.LogInfo($"[{_logTag}] Subscribed to {settingName}");
        }
    }

    private uint OnSettingChangedNative(IntPtr context, uint type, IntPtr setting)
    {
        if (type == PowerSettingsNative.PowerSettingChangeNotification && setting != IntPtr.Zero)
        {
            _onSettingChanged(setting);
        }

        return 0;
    }

    public void Dispose()
    {
        if (_registration != IntPtr.Zero)
        {
            try
            {
                uint result = PowerSettingsNative.PowerSettingUnregisterNotification(_registration);
                if (result != 0)
                {
                    Logger.LogWarning($"[{_logTag}] PowerSettingUnregisterNotification failed: 0x{result:X}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{_logTag}] Unregister failed: {ex.Message}");
            }

            _registration = IntPtr.Zero;
        }

        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }
    }
}
