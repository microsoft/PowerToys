// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Helpers;

/// <summary>
/// Minimal AOT-compatible shell interop used to enumerate and invoke
/// Control Panel task items (the same items Control Panel's own search and
/// the shell "All Tasks" folder expose).
/// </summary>
internal static partial class ShellInterop
{
    /// <summary>
    /// Shell "All Tasks" folder containing every Control Panel task. The
    /// "shell:" prefix is required — this namespace cannot be parsed as a
    /// plain desktop-relative name (SHCreateItemFromParsingName returns
    /// E_INVALIDARG without it).
    /// </summary>
    internal const string AllTasksFolderParsingName = "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}";

    /// <summary>BHID_EnumItems binding handler id.</summary>
    internal static readonly Guid BHIDEnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");

    internal static readonly Guid IShellItemIid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    internal static readonly Guid IEnumShellItemsIid = new("70629033-e363-4a28-a567-0db78006e6d7");

    /// <summary>
    /// PKEY_ApplicationName — for Control Panel task items this is the name
    /// of the Control Panel applet the task belongs to (e.g. "Power Options"),
    /// localized by the shell. Shown in Explorer's All Tasks folder as the
    /// group heading.
    /// </summary>
    internal static readonly PropertyKey PKeyApplicationName = new() { FmtId = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), Pid = 18 };

    /// <summary>
    /// PKEY_Keywords — the localized search keywords Control Panel's own
    /// search uses to match a task (e.g. "joystick" for "Set up USB game
    /// controllers"). IShellItem2.GetString returns them joined with "; ".
    /// </summary>
    internal static readonly PropertyKey PKeyKeywords = new() { FmtId = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), Pid = 5 };

    // SIGDN values (https://learn.microsoft.com/windows/win32/api/shobjidl_core/ne-shobjidl_core-sigdn)
    internal const uint SIGDNNormalDisplay = 0x00000000;
    internal const uint SIGDNDesktopAbsoluteParsing = 0x80028000;

    // SHELLEXECUTEINFOW.fMask flags
    internal const uint SEEMaskIdList = 0x00000004;
    internal const uint SEEMaskNoAsync = 0x00000100;

    internal const int SWShowNormal = 1;

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, in Guid riid, out IntPtr ppv);

    [LibraryImport("shell32.dll")]
    internal static partial int SHGetIDListFromObject(IntPtr punk, out IntPtr ppidl);

    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShellExecuteEx(ref SHELLEXECUTEINFOW lpExecInfo);

    /// <summary>
    /// Creates an <see cref="IShellItem"/> from a shell parsing name (e.g. "::{CLSID}\...").
    /// Returns null when the item cannot be created.
    /// </summary>
    internal static IShellItem CreateItemFromParsingName(string parsingName)
    {
        var hr = SHCreateItemFromParsingName(parsingName, IntPtr.Zero, IShellItemIid, out var ptr);
        if (hr != 0 || ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return (IShellItem)ComWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    /// <summary>
    /// Binds the given shell folder item to its item enumerator.
    /// Returns null when the folder cannot be enumerated.
    /// </summary>
    internal static IEnumShellItems GetItemEnumerator(IShellItem folder)
    {
        var hr = folder.BindToHandler(IntPtr.Zero, BHIDEnumItems, IEnumShellItemsIid, out var ptr);
        if (hr != 0 || ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return (IEnumShellItems)ComWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    /// <summary>
    /// Copies a native item id list (PIDL) into a managed byte array. A PIDL
    /// is a sequence of SHITEMID blocks { ushort cb; byte[cb - 2]; }
    /// terminated by cb == 0.
    /// </summary>
    internal static byte[] CopyIdList(IntPtr pidl)
    {
        var size = 0;
        while (true)
        {
            var cb = (ushort)Marshal.ReadInt16(pidl, size);
            if (cb == 0)
            {
                size += sizeof(ushort);
                break;
            }

            size += cb;
        }

        var bytes = new byte[size];
        Marshal.Copy(pidl, bytes, 0, size);
        return bytes;
    }

    /// <summary>
    /// Invokes a shell item from its stored id list bytes via ShellExecuteEx.
    /// Control Panel task items have no file-system path or executable
    /// command line and cannot be re-parsed by name, so their id list is
    /// captured during enumeration and used here to launch them.
    /// </summary>
    internal static bool InvokeShellItemByIdList(byte[] idList)
    {
        if (idList is null || idList.Length == 0)
        {
            return false;
        }

        var pidl = Marshal.AllocCoTaskMem(idList.Length);
        try
        {
            Marshal.Copy(idList, 0, pidl, idList.Length);

            var info = new SHELLEXECUTEINFOW
            {
                CbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFOW>(),
                FMask = SEEMaskIdList | SEEMaskNoAsync,
                LpIDList = pidl,
                Show = SWShowNormal,
            };

            return ShellExecuteEx(ref info);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    /// <summary>
    /// Wraps a raw IShellItem pointer (e.g. from IEnumShellItems.Next) into a
    /// managed <see cref="IShellItem"/>. Does not consume the caller's
    /// reference — the caller still has to release the raw pointer.
    /// </summary>
    internal static IShellItem CreateShellItemFromPointer(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return null;
        }

        return (IShellItem)ComWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
    }

    /// <summary>
    /// A shell property key (PROPERTYKEY from wtypes.h).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid FmtId;
        public uint Pid;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHELLEXECUTEINFOW
    {
        public uint CbSize;
        public uint FMask;
        public IntPtr Hwnd;
        public IntPtr LpVerb;
        public IntPtr LpFile;
        public IntPtr LpParameters;
        public IntPtr LpDirectory;
        public int Show;
        public IntPtr HInstApp;
        public IntPtr LpIDList;
        public IntPtr LpClass;
        public IntPtr HkeyClass;
        public uint DwHotKey;
        public IntPtr HIconOrMonitor;
        public IntPtr Process;
    }
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetParent(out IntPtr ppsi);

    [PreserveSig]
    int GetDisplayName(uint sigdnName, out string ppszName);

    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int Compare(IntPtr psi, uint hint, out int piOrder);
}

/// <summary>
/// IShellItem2 — extends IShellItem with property access. Only GetString is
/// used; the other methods are declared to keep the vtable layout of
/// ShObjIdl_core.h intact.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93")]
internal partial interface IShellItem2 : IShellItem
{
    [PreserveSig]
    int GetPropertyStore(uint flags, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetPropertyStoreWithCreateObject(uint flags, IntPtr punkCreateObject, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, uint flags, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetPropertyDescriptionList(in ShellInterop.PropertyKey keyType, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int Update(IntPtr pbc);

    [PreserveSig]
    int GetProperty(in ShellInterop.PropertyKey key, IntPtr ppropvar);

    [PreserveSig]
    int GetCLSID(in ShellInterop.PropertyKey key, out Guid pclsid);

    [PreserveSig]
    int GetFileTime(in ShellInterop.PropertyKey key, out ulong pft);

    [PreserveSig]
    int GetInt32(in ShellInterop.PropertyKey key, out int pi);

    [PreserveSig]
    int GetString(in ShellInterop.PropertyKey key, out string ppsz);

    [PreserveSig]
    int GetUInt32(in ShellInterop.PropertyKey key, out uint pui);

    [PreserveSig]
    int GetUInt64(in ShellInterop.PropertyKey key, out ulong pull);

    [PreserveSig]
    int GetBool(in ShellInterop.PropertyKey key, out int pf);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("70629033-e363-4a28-a567-0db78006e6d7")]
internal partial interface IEnumShellItems
{
    [PreserveSig]
    int Next(uint celt, out IntPtr rgelt, out uint pceltFetched);

    [PreserveSig]
    int Skip(uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out IntPtr ppenum);
}
