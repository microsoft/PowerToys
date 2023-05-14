using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICMetadataBlockWriter)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICMetadataBlockWriter : IWICMetadataBlockReader
    {
        #region Members inherited from `IWICMetadataBlockReader`

        new Guid GetContainerFormat();

        new int GetCount();

        new IWICMetadataReader GetReaderByIndex(
            [In] int nIndex);

        new IEnumUnknown GetEnumerator();

        #endregion

        void InitializeFromBlockReader(
            [In] IWICMetadataBlockReader pIMDBlockReader);

        IWICMetadataWriter GetWriterByIndex(
            [In] int nIndex);

        void AddWriter(
            [In] IWICMetadataWriter pIMetadataWriter);

        void SetWriterByIndex(
            [In] int nIndex,
            [In] IWICMetadataWriter pIMetadataWriter);

        void RemoveWriterByIndex(
            [In] int nIndex);
    }
}
