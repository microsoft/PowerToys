// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public class ShellGetFolder
    {
        public delegate int BrowseCallbackProc(IntPtr hwnd, int msg, IntPtr lp, IntPtr wp);

        [StructLayout(LayoutKind.Sequential)]
        public struct BrowseInformation
        {
            public IntPtr HwndOwner;
            public IntPtr PidlRoot;
            public string PszDisplayName;
            public string LpszTitle;
            public uint UlFlags;
            public BrowseCallbackProc Lpfn;
            public IntPtr LParam;
            public int IImage;
        }

        public static string GetFolderDialog(IntPtr hwndOwner)
        {
            // windows MAX_PATH with long path enable can be approximated 32k char long
            // allocating more than double (unicode) to hold the path
            StringBuilder sb = new StringBuilder(65000);
            IntPtr bufferAddress = Marshal.AllocHGlobal(65000);
            IntPtr pidl = IntPtr.Zero;
            BrowseInformation browseInfo;
            browseInfo.HwndOwner = hwndOwner;
            browseInfo.PidlRoot = IntPtr.Zero;
            browseInfo.PszDisplayName = null;
            browseInfo.LpszTitle = null;
            browseInfo.UlFlags = 0;
            browseInfo.Lpfn = null;
            browseInfo.LParam = IntPtr.Zero;
            browseInfo.IImage = 0;

            try
            {
                pidl = NativeMethods.SHBrowseForFolderW(ref browseInfo);
                if (NativeMethods.SHGetPathFromIDListW(pidl, bufferAddress) == 0)
                {
                    return null;
                }

                sb.Append(Marshal.PtrToStringUni(bufferAddress));
                Marshal.FreeHGlobal(bufferAddress);
            }
            finally
            {
                // Need to free pidl
                Marshal.FreeCoTaskMem(pidl);
            }

            return sb.ToString();
        }
    }
}
