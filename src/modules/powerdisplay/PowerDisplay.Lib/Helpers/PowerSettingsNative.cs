// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Helpers;

/// <summary>
/// P/Invoke surface for the console-display-state power-setting notification.
/// Holds the Powrprof.dll bindings, GUIDs, and structures used by the
/// PowerDisplay module's display-change watcher. No business logic lives here.
///
/// Uses <see cref="LibraryImportAttribute"/> (source-generated, AOT-compatible)
/// matching the convention established in <c>PowerDisplay.Common.Drivers.PInvoke</c>.
/// </summary>
[SuppressMessage(
    "Interoperability",
    "CA1401:P/Invokes should not be visible",
    Justification = "Exposed for cross-assembly use by PowerToys.PowerDisplay; the main app subscribes/unsubscribes the notification from DisplayChangeWatcher. Pattern matches cmdpal/launcher NativeMethods.cs.")]
public static partial class PowerSettingsNative
{
    /// <summary>
    /// GUID_CONSOLE_DISPLAY_STATE — Windows fires this whenever the console
    /// display's power state changes. Data byte is 0 (off), 1 (on), 2 (dimmed).
    /// Reliable on both S3 and S0ix, and also fires for idle-blank / lid /
    /// screensaver transitions that never enter system sleep.
    /// </summary>
    public static readonly Guid GuidConsoleDisplayState =
        new("6fe69556-704a-47a0-8f24-c28d936fda47");

    /// <summary>
    /// Console display state values. Typed <c>uint</c> to match the read-then-widen
    /// pattern in <c>DisplayChangeWatcher</c> (single-byte payload read via
    /// <see cref="Marshal.ReadByte(IntPtr, int)"/> and stored in a <c>uint</c>
    /// field for comparison).
    /// </summary>
    public const uint DisplayStateOff = 0;
    public const uint DisplayStateOn = 1;
    public const uint DisplayStateDimmed = 2;

    /// <summary>
    /// DEVICE_NOTIFY_CALLBACK — Recipient is a pointer to
    /// DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS containing a callback delegate.
    /// </summary>
    public const uint DeviceNotifyCallback = 0x00000002;

    /// <summary>
    /// Power-broadcast type value passed as the <c>type</c> parameter of the
    /// callback when a subscribed power setting changes.
    /// </summary>
    public const uint PowerSettingChangeNotification = 0x8013;

    /// <summary>
    /// Callback signature for power-setting notifications.
    /// Windows invokes this on an internal worker thread — implementations
    /// must marshal to their own thread before touching UI / VM state.
    /// </summary>
    /// <remarks>
    /// <see cref="Marshal.GetFunctionPointerForDelegate"/> does NOT root the
    /// delegate against GC. Callers must keep the managed delegate alive (e.g.
    /// via <c>GCHandle.Alloc</c>) for the entire lifetime of the registration,
    /// or Windows will eventually invoke a collected delegate and produce an
    /// access violation.
    /// </remarks>
    /// <param name="context">Opaque context supplied at registration.</param>
    /// <param name="type">Notification type (e.g. <see cref="PowerSettingChangeNotification"/>).</param>
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

    /// <summary>
    /// Registers a callback to receive notifications when a specific power
    /// setting changes. Free the registration with
    /// <see cref="PowerSettingUnregisterNotification"/> when no longer needed.
    /// </summary>
    /// <returns>
    /// <c>ERROR_SUCCESS</c> (0) on success; otherwise a Win32 error code
    /// (the function returns the error directly; <c>GetLastError</c> is not used).
    /// </returns>
    [LibraryImport("Powrprof.dll")]
    public static partial uint PowerSettingRegisterNotification(
        ref Guid settingGuid,
        uint flags,
        ref DeviceNotifySubscribeParameters recipient,
        out IntPtr registrationHandle);

    /// <summary>
    /// Cancels a registration previously returned by
    /// <see cref="PowerSettingRegisterNotification"/>.
    /// </summary>
    /// <returns>
    /// <c>ERROR_SUCCESS</c> (0) on success; otherwise a Win32 error code.
    /// </returns>
    [LibraryImport("Powrprof.dll")]
    public static partial uint PowerSettingUnregisterNotification(IntPtr registrationHandle);
}
