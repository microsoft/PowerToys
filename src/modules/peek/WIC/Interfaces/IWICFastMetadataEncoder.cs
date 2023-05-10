using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICFastMetadataEncoder)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICFastMetadataEncoder
    {
        void Commit();

        IWICMetadataQueryWriter GetMetadataQueryWriter();
    }
}
