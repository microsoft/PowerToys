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
    private const int UpdateCacheIntervalSeconds = 5;
    private static bool cachedRebootRequired;
    private static DateTime timeOfLastQuery;

    /// <summary>
    /// Gets a value indicating whether Windows Update requires a restart to finish
    /// installing updates, i.e. whether the Start menu would show "Update and restart".
    /// Returns false if the state cannot be determined.
    /// </summary>
    public static bool IsUpdatePending()
    {
        if ((DateTime.Now - timeOfLastQuery).TotalSeconds < UpdateCacheIntervalSeconds)
        {
            return cachedRebootRequired;
        }

        timeOfLastQuery = DateTime.Now;

        try
        {
            var hr = CoCreateInstance(in SystemInformationClsid, IntPtr.Zero, ClsCtxInprocServer, in SystemInformationIid, out var instance);
            if (hr < 0)
            {
                cachedRebootRequired = false;
                return false;
            }

            try
            {
                var systemInformation = (ISystemInformation)ComWrappers.GetOrCreateObjectForComInstance(instance, CreateObjectFlags.None);
                cachedRebootRequired = systemInformation.GetRebootRequired();
            }
            finally
            {
                Marshal.Release(instance);
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Failed to query Windows Update reboot state: {ex.Message}" });
            cachedRebootRequired = false;
        }

        return cachedRebootRequired;
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
    public static bool InitiateUpdateShutdown(bool restart)
    {
        // InitiateShutdown requires the (normally disabled) shutdown privilege on the token.
        EnableShutdownPrivilege();

        var result = PInvoke.InitiateShutdown(null, null, 0, (SHUTDOWN_FLAGS)GetUpdateShutdownFlags(restart), ShutdownReasonPlannedOsUpgrade);
        if (result != 0)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"InitiateShutdown failed with Win32 error {result}" });
            return false;
        }

        return true;
    }

    private static unsafe void EnableShutdownPrivilege()
    {
        HANDLE token;
        if (!PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
        {
            return;
        }

        try
        {
            if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
            {
                return;
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

            _ = PInvoke.AdjustTokenPrivileges(token, false, &privileges, 0, null, null);
        }
        finally
        {
            _ = NativeMethods.CloseHandle(token);
        }
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
