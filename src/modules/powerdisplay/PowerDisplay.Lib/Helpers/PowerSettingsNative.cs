// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Helpers;

/// <summary>
/// P/Invoke surface for the console-display-state power-setting notification.
/// Centralises the powrprof.dll bindings, GUIDs, and structures used by the
/// PowerDisplay module's display-change watcher. No business logic lives here.
/// </summary>
public static class PowerSettingsNative
{
    /// <summary>
    /// GUID_CONSOLE_DISPLAY_STATE — Windows fires this whenever the console
    /// display's power state changes. Data byte is 0 (off), 1 (on), 2 (dimmed).
    /// Reliable on both S3 and S0ix, and also fires for idle-blank / lid /
    /// screensaver transitions that never enter system sleep.
    /// </summary>
    public static readonly Guid GuidConsoleDisplayState =
        new("6fe69556-704a-47a0-8f24-c28d936fda47");

    public const uint DisplayStateOff = 0;
    public const uint DisplayStateOn = 1;
    public const uint DisplayStateDimmed = 2;

    /// <summary>
    /// DEVICE_NOTIFY_CALLBACK — Recipient is a pointer to
    /// DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS containing a callback delegate.
    /// </summary>
    public const uint DeviceNotifyCallback = 0x00000002;

    /// <summary>
    /// PBT_POWERSETTINGCHANGE — the Type parameter of the callback when a
    /// subscribed power setting changes.
    /// </summary>
    public const uint PbtPowerSettingChange = 0x8013;

    /// <summary>
    /// Callback signature for power-setting notifications.
    /// Windows invokes this on an internal worker thread — implementations
    /// must marshal to their own thread before touching UI / VM state.
    /// </summary>
    /// <param name="context">Opaque context supplied at registration.</param>
    /// <param name="type">Notification type (e.g. PBT_POWERSETTINGCHANGE).</param>
    /// <param name="setting">Pointer to a POWERBROADCAST_SETTING.</param>
    /// <returns>Reserved — must return 0.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate uint DeviceNotifyCallbackRoutine(IntPtr context, uint type, IntPtr setting);

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceNotifySubscribeParameters
    {
        public IntPtr Callback;
        public IntPtr Context;
    }

    /// <summary>
    /// POWERBROADCAST_SETTING — the payload Windows passes to the callback.
    /// PowerSetting (16 bytes) + DataLength (4 bytes) precede a variable-length
    /// Data array whose first byte holds the new display state.
    /// </summary>
    public const int PowerBroadcastSettingDataOffset = 20;

#pragma warning disable CA1401 // P/Invokes should not be visible — exposed because PowerDisplay.exe (separate assembly) registers/unregisters the notification.
    [DllImport("powrprof.dll", SetLastError = false)]
    public static extern uint PowerSettingRegisterNotification(
        ref Guid settingGuid,
        uint flags,
        ref DeviceNotifySubscribeParameters recipient,
        out IntPtr registrationHandle);

    [DllImport("powrprof.dll", SetLastError = false)]
    public static extern uint PowerSettingUnregisterNotification(IntPtr registrationHandle);
#pragma warning restore CA1401 // P/Invokes should not be visible
}
