using System;
using System.Runtime.InteropServices;

namespace ExplorerCommandLib.Interop
{
    [ComImport]
    [Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumExplorerCommand
    {
        [PreserveSig]
        int Next(uint elementCount, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] out IExplorerCommand[] commands, out uint fetched);
        void Skip(uint count);
        void Reset();
        void Clone(out IEnumExplorerCommand copy);
    }
}
