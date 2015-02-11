using System;
using System.Runtime.InteropServices;

namespace Wox.ShellContext
{
    [ComImport(),
    Guid("000214F2-0000-0000-C000-000000000046"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumIDList
    {
        [PreserveSig()]
        uint Next(
            uint celt,
            out IntPtr rgelt,
            out int pceltFetched);

        void Skip(
            uint celt);

        void Reset();

        IEnumIDList Clone();
    }
}
