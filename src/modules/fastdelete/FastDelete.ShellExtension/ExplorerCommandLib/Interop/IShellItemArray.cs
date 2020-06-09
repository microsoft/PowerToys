using System;
using System.Runtime.InteropServices;

namespace ExplorerCommandLib.Interop
{
    [ComImport]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        [Obsolete("Not implemented", true)]
        void BindToHandler();
        [Obsolete("Not implemented", true)]
        void GetPropertyStore();
        [Obsolete("Not implemented", true)]
        void GetPropertyDescriptionList();
        [Obsolete("Not implemented", true)]
        void GetAttributes();
        void GetCount(out int count);
        void GetItemAt(int index, out IShellItem item);
        [Obsolete("Not implemented", true)]
        void EnumItems();
    }
}