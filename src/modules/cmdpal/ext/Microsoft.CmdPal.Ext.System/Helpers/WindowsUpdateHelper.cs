// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Shutdown;

namespace Microsoft.CmdPal.Ext.System.Helpers;

/// <summary>
/// Detects whether Windows Update is waiting for a restart to finish installing updates
/// and initiates the "update and restart" / "update and shut down" actions.
/// </summary>
internal static partial class WindowsUpdateHelper
{
    // SHTDN_REASON_FLAG_PLANNED | SHTDN_REASON_MAJOR_OPERATINGSYSTEM | SHTDN_REASON_MINOR_UPGRADE
    private const SHUTDOWN_REASON ShutdownReasonPlannedOsUpgrade =
        SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED | SHUTDOWN_REASON.SHTDN_REASON_MAJOR_OPERATINGSYSTEM | SHUTDOWN_REASON.SHTDN_REASON_MINOR_UPGRADE;

    private const uint ClsCtxInprocServer = 0x1;

    // WUAPI SystemInformation coclass (wuapi.idl)
    private static readonly Guid SystemInformationClsid = new("C01B9BA0-BEA7-41BA-B604-D0A36F469133");
    private static readonly Guid SystemInformationIid = new("ADE87BF7-7B56-4275-8FAB-B9B0E591844B");

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    // Cache the WUAPI answer for a few seconds so that per-keystroke queries don't
    // repeatedly instantiate the COM object (same approach as the network info cache).
    private const long UpdateCacheIntervalMs = 5000;
    private static readonly object CacheLock = new();
    private static bool cachedRebootRequired;
    private static bool hasCachedValue;

    // Monotonic timestamp (Environment.TickCount64) of the last successful query, so a
    // wall-clock change can't make the cache look newer or older than it really is.
    private static long lastQueryTicks;

    /// <summary>
    /// Gets a value indicating whether Windows Update requires a restart to finish
    /// installing updates, i.e. whether the Start menu would show "Update and restart".
    /// Returns false if the state cannot be determined.
    /// </summary>
    public static bool IsUpdatePending()
    {
        // The whole check-and-refresh runs under the lock so the timestamp and the value
        // are always published together; otherwise a concurrent caller could read a stale
        // value while the timestamp already claims it is fresh.
        lock (CacheLock)
        {
            var now = Environment.TickCount64;
            if (hasCachedValue && (now - lastQueryTicks) < UpdateCacheIntervalMs)
            {
                return cachedRebootRequired;
            }

            cachedRebootRequired = QueryRebootRequired();
            lastQueryTicks = Environment.TickCount64;
            hasCachedValue = true;
            return cachedRebootRequired;
        }
    }

    private static bool QueryRebootRequired()
    {
        try
        {
            var hr = CoCreateInstance(in SystemInformationClsid, IntPtr.Zero, ClsCtxInprocServer, in SystemInformationIid, out var instance);
            if (hr < 0)
            {
                return false;
            }

            try
            {
                var systemInformation = (ISystemInformation)ComWrappers.GetOrCreateObjectForComInstance(instance, CreateObjectFlags.None);
                return systemInformation.GetRebootRequired();
            }
            finally
            {
                Marshal.Release(instance);
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Failed to query Windows Update reboot state: {ex.Message}" });
            return false;
        }
    }

    /// <summary>
    /// Returns the InitiateShutdown flags for an "update and restart" (true) or
    /// "update and shut down" (false) request.
    /// </summary>
    public static uint GetUpdateShutdownFlags(bool restart)
        => (uint)(SHUTDOWN_FLAGS.SHUTDOWN_INSTALL_UPDATES | (restart ? SHUTDOWN_FLAGS.SHUTDOWN_RESTART : SHUTDOWN_FLAGS.SHUTDOWN_POWEROFF));

    /// <summary>
    /// Installs pending updates and restarts (true) or shuts down (false) the computer.
    /// </summary>
    /// <returns>True if the system accepted the shutdown request.</returns>
    public static unsafe bool InitiateUpdateShutdown(bool restart)
    {
        HANDLE token;
        if (!PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"OpenProcessToken failed with Win32 error {Marshal.GetLastPInvokeError()}" });
            return false;
        }

        try
        {
            // InitiateShutdown requires the (normally disabled) shutdown privilege. Enable it,
            // remembering the previous state so we can put it back afterwards instead of
            // leaving it enabled for the rest of the process lifetime.
            if (!TryEnableShutdownPrivilege(token, out var previousState, out var hasPreviousState))
            {
                return false;
            }

            try
            {
                var result = PInvoke.InitiateShutdown(null, null, 0, (SHUTDOWN_FLAGS)GetUpdateShutdownFlags(restart), ShutdownReasonPlannedOsUpgrade);
                if (result != 0)
                {
                    ExtensionHost.LogMessage(new LogMessage() { Message = $"InitiateShutdown failed with Win32 error {result}" });
                    return false;
                }

                return true;
            }
            finally
            {
                if (hasPreviousState)
                {
                    _ = PInvoke.AdjustTokenPrivileges(token, false, &previousState, (uint)sizeof(TOKEN_PRIVILEGES), null, null);
                }
            }
        }
        finally
        {
            _ = NativeMethods.CloseHandle(token);
        }
    }

    /// <summary>
    /// Enables <c>SeShutdownPrivilege</c> on the given token, returning the prior state so
    /// the caller can restore it. Returns false (and logs) if the privilege could not be
    /// assigned — <c>AdjustTokenPrivileges</c> reports success even then, so the last error
    /// must be checked for <c>ERROR_NOT_ALL_ASSIGNED</c>.
    /// </summary>
    private static unsafe bool TryEnableShutdownPrivilege(HANDLE token, out TOKEN_PRIVILEGES previousState, out bool hasPreviousState)
    {
        previousState = default;
        hasPreviousState = false;

        if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"LookupPrivilegeValue failed with Win32 error {Marshal.GetLastPInvokeError()}" });
            return false;
        }

        var privileges = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
        };
        privileges.Privileges[0] = new LUID_AND_ATTRIBUTES
        {
            Luid = luid,
            Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED,
        };

        TOKEN_PRIVILEGES prior;
        uint returnLength;
        var adjusted = PInvoke.AdjustTokenPrivileges(token, false, &privileges, (uint)sizeof(TOKEN_PRIVILEGES), &prior, &returnLength);
        var lastError = Marshal.GetLastPInvokeError();

        if (!adjusted || lastError == (int)WIN32_ERROR.ERROR_NOT_ALL_ASSIGNED)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Failed to enable SeShutdownPrivilege (Win32 error {lastError})" });
            return false;
        }

        previousState = prior;
        hasPreviousState = returnLength > 0;
        return true;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);
}

/// <summary>
/// WUAPI ISystemInformation (wuapi.idl). This is a dual interface; the first four
/// methods are placeholders occupying the IDispatch vtable slots and must never be
/// called. The remaining members are called through the vtable, which dual interfaces
/// populate alongside IDispatch.
/// </summary>
[GeneratedComInterface]
[Guid("ADE87BF7-7B56-4275-8FAB-B9B0E591844B")]
internal partial interface ISystemInformation
{
    // IDispatch slots — placeholders, do not call.
    void GetTypeInfoCountPlaceholder(nint pctinfo);

    void GetTypeInfoPlaceholder(uint iTInfo, uint lcid, nint ppTInfo);

    void GetIDsOfNamesPlaceholder(nint riid, nint rgszNames, uint cNames, uint lcid, nint rgDispId);

    void InvokePlaceholder(int dispIdMember, nint riid, uint lcid, ushort wFlags, nint pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr);

    // ISystemInformation members, in vtable order.
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetOemHardwareSupportLink();

    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool GetRebootRequired();
}
