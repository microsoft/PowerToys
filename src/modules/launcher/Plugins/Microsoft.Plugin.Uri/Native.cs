using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Plugin.Uri
{
    internal static class NativeMethods
    {
        internal enum Hresult : uint
        {
            Ok = 0x0000,
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        internal static extern Hresult SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf,
            IntPtr ppvReserved);
    }
}
