// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Explorer;

// Interface layouts and IDs match ShObjIdl_core.h in the Windows SDK.
public static class ShellInterop
{
    public const string CommandClassId = ExplorerRegistration.CommandClassId;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CommandPosition
    {
        public readonly int X;
        public readonly int Y;
    }

    [ComVisible(true)]
    [Guid("7F9185B0-CB92-43C5-80A9-92277A4F7B54")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExecuteCommand
    {
        [PreserveSig]
        int SetKeyState(uint keyState);

        [PreserveSig]
        int SetParameters([MarshalAs(UnmanagedType.LPWStr)] string parameters);

        [PreserveSig]
        int SetPosition(CommandPosition position);

        [PreserveSig]
        int SetShowWindow(int show);

        [PreserveSig]
        int SetNoShowUI([MarshalAs(UnmanagedType.Bool)] bool noShow);

        [PreserveSig]
        int SetDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

        [PreserveSig]
        int Execute();
    }

    [ComVisible(true)]
    [Guid("1C9CD5BB-98E9-4491-A60F-31AACC72B83C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IObjectWithSelection
    {
        [PreserveSig]
        int SetSelection([MarshalAs(UnmanagedType.Interface)] IShellItemArray? selection);

        [PreserveSig]
        int GetSelection(ref Guid interfaceId, out IntPtr result);
    }

    [ComImport]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemArray
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid interfaceId, out IntPtr result);

        void GetPropertyStore(uint flags, ref Guid interfaceId, out IntPtr result);

        void GetPropertyDescriptionList(IntPtr propertyKey, ref Guid interfaceId, out IntPtr result);

        void GetAttributes(uint flags, uint mask, out uint attributes);

        void GetCount(out uint count);

        void GetItemAt(uint index, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        void EnumItems(out IntPtr enumerator);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid interfaceId, out IntPtr result);

        void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);

        void GetDisplayName(uint format, out IntPtr name);

        void GetAttributes(uint mask, out uint attributes);

        void Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
    }

    [ComVisible(true)]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance);

        [PreserveSig]
        int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
    }

    internal static int QueryInterface(object value, ref Guid interfaceId, out IntPtr result)
    {
        var unknown = Marshal.GetIUnknownForObject(value);
        try
        {
            return Marshal.QueryInterface(unknown, in interfaceId, out result);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    [DllImport("ole32.dll")]
    internal static extern int CoRegisterClassObject(ref Guid classId, IntPtr factory, uint context, uint flags, out uint cookie);

    [DllImport("ole32.dll")]
    internal static extern int CoRevokeClassObject(uint cookie);

    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(ref Guid classId, IntPtr outer, uint context, ref Guid interfaceId, out IntPtr instance);

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();
}
