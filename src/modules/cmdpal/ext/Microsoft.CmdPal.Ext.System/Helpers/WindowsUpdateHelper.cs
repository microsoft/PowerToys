// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Helpers;

/// <summary>
/// Detects whether Windows Update is waiting for a restart to finish installing updates
/// and initiates the "update and restart" / "update and shut down" actions.
/// </summary>
internal static partial class WindowsUpdateHelper
{
    // InitiateShutdown flags (winreg.h)
    internal const uint ShutdownRestart = 0x00000004;
    internal const uint ShutdownPoweroff = 0x00000008;
    internal const uint ShutdownInstallUpdates = 0x00000040;

    // SHTDN_REASON_FLAG_PLANNED | SHTDN_REASON_MAJOR_OPERATINGSYSTEM | SHTDN_REASON_MINOR_UPGRADE
    internal const uint ShutdownReasonPlannedOsUpgrade = 0x80000000 | 0x00020000 | 0x00000003;

    private const uint ClsCtxInprocServer = 0x1;
    private const uint TokenAdjustPrivileges = 0x20;
    private const uint TokenQuery = 0x8;
    private const uint SePrivilegeEnabled = 0x2;
    private const string SeShutdownName = "SeShutdownPrivilege";

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
        => ShutdownInstallUpdates | (restart ? ShutdownRestart : ShutdownPoweroff);

    /// <summary>
    /// Installs pending updates and restarts (true) or shuts down (false) the computer.
    /// </summary>
    /// <returns>True if the system accepted the shutdown request.</returns>
    public static bool InitiateUpdateShutdown(bool restart)
    {
        // InitiateShutdown requires the (normally disabled) shutdown privilege on the token.
        EnableShutdownPrivilege();

        var result = InitiateShutdown(null, null, 0, GetUpdateShutdownFlags(restart), ShutdownReasonPlannedOsUpgrade);
        if (result != 0)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"InitiateShutdown failed with Win32 error {result}" });
            return false;
        }

        return true;
    }

    private static void EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
        {
            return;
        }

        try
        {
            if (!LookupPrivilegeValue(null, SeShutdownName, out var luid))
            {
                return;
            }

            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SePrivilegeEnabled,
            };

            _ = AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            _ = NativeMethods.CloseHandle(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);

    [LibraryImport("advapi32.dll", EntryPoint = "InitiateShutdownW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint InitiateShutdown(string? lpMachineName, string? lpMessage, uint dwGracePeriod, uint dwShutdownFlags, uint dwReason);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeValue(string? lpSystemName, string lpName, out Luid lpLuid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustTokenPrivileges(IntPtr tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, ref TokenPrivileges newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);
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
