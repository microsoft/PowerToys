#nullable enable

using System;
using System.Runtime.InteropServices;

namespace ExplorerCommandLib.Interop
{
    [ComImport]
    [Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IExplorerCommand
    {
        void GetTitle(IShellItemArray itemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? title);
        void GetIcon(IShellItemArray itemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? resourceString);
        void GetToolTip(IShellItemArray itemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? tooltip);
        void GetCanonicalName(out Guid guid);
        void GetState(IShellItemArray itemArray, [MarshalAs(UnmanagedType.Bool)] bool okToBeShow, out ExplorerCommandState commandState);
        void Invoke(IShellItemArray itemArray, [MarshalAs(UnmanagedType.Interface)] object bindCtx);
        void GetFlags(out int flags);
        [PreserveSig]
        int EnumSubCommands(out IEnumExplorerCommand? commandEnum);
    }
}
