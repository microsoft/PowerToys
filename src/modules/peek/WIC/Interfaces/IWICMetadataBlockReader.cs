using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICMetadataBlockReader)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICMetadataBlockReader
    {
        Guid GetContainerFormat();

        int GetCount();

        IWICMetadataReader GetReaderByIndex(
            [In] int nIndex);

        IEnumUnknown GetEnumerator();
    }
}
