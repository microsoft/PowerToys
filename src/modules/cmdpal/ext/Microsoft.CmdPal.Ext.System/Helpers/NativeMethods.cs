// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

#pragma warning disable SA1649, CA1051, CA1707, CA1028, CA1714, CA1069, SA1402

namespace Microsoft.CmdPal.Ext.System.Helpers;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "We want plugins to share this NativeMethods class, instead of each one creating its own.")]
public static class NativeMethods
{
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFirmwareType(ref FirmwareType FirmwareType);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("user32")]
    public static extern void LockWorkStation();

    [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint SHEmptyRecycleBin(IntPtr hWnd, uint dwFlags);
}

public enum HRESULT : uint
{
    /// <summary>
    /// Operation successful.
    /// </summary>
    S_OK = 0x00000000,

    /// <summary>
    /// Operation successful. (negative condition/no operation)
    /// </summary>
    S_FALSE = 0x00000001,

    /// <summary>
    /// Not implemented.
    /// </summary>
    E_NOTIMPL = 0x80004001,

    /// <summary>
    /// No such interface supported.
    /// </summary>
    E_NOINTERFACE = 0x80004002,

    /// <summary>
    /// Pointer that is not valid.
    /// </summary>
    E_POINTER = 0x80004003,

    /// <summary>
    /// Operation aborted.
    /// </summary>
    E_ABORT = 0x80004004,

    /// <summary>
    /// Unspecified failure.
    /// </summary>
    E_FAIL = 0x80004005,

    /// <summary>
    /// Unexpected failure.
    /// </summary>
    E_UNEXPECTED = 0x8000FFFF,

    /// <summary>
    /// General access denied error.
    /// </summary>
    E_ACCESSDENIED = 0x80070005,

    /// <summary>
    /// Handle that is not valid.
    /// </summary>
    E_HANDLE = 0x80070006,

    /// <summary>
    /// Failed to allocate necessary memory.
    /// </summary>
    E_OUTOFMEMORY = 0x8007000E,

    /// <summary>
    /// One or more arguments are not valid.
    /// </summary>
    E_INVALIDARG = 0x80070057,

    /// <summary>
    /// The operation was canceled by the user. (Error source 7 means Win32.)
    /// </summary>
    /// <SeeAlso href="https://learn.microsoft.com/windows/win32/debug/system-error-codes--1000-1299-"/>
    /// <SeeAlso href="https://en.wikipedia.org/wiki/HRESULT"/>
    E_CANCELLED = 0x800704C7,
}

/// <remarks>
/// <see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-firmware_type">see learn.microsoft.com</see>
/// </remarks>
public enum FirmwareType
{
    Unknown = 0,
    Bios = 1,
    Uefi = 2,
    Max = 3,
}

public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
