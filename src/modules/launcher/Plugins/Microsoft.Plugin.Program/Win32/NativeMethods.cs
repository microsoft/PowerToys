// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using static Microsoft.Plugin.Program.Programs.UWP;

namespace Microsoft.Plugin.Program.Win32
{
    internal class NativeMethods
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        internal static extern Hresult SHCreateStreamOnFileEx(string fileName, Stgm grfMode, uint attributes, bool create, IStream reserved, out IStream stream);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        internal static extern Hresult SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);
    }
}
