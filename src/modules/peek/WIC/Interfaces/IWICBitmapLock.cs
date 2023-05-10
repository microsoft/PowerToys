using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICBitmapLock)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmapLock
    {
        void GetSize(
            [Out] out int puiWidth,
            [Out] out int puiHeight);

        int GetStride();

        IntPtr GetDataPointer( // byte*
            [Out] out int pcbBufferSize);

        Guid GetPixelFormat();
    }
}
