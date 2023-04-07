// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace RegistryPreview
{
    // Workaround for broken FileOpenPicker while running as admin, per:
    // https://github.com/microsoft/WindowsAppSDK/issues/2504https://github.com/microsoft/WindowsAppSDK/issues/2504
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct OpenFileName
    {
#pragma warning disable SA1307 // relax casing rule, for external APIs
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
#pragma warning restore SA1307 // relax casing rule, for external APIs
    }

    public static partial class FilePicker
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        public static string ShowDialog(string startingDirectory, string[] filters, string filterName, string dialogTitle)
        {
            OpenFileName ofn = default(OpenFileName);
            ofn.lStructSize = Marshal.SizeOf(ofn);

            ofn.lpstrFilter = filterName;
            foreach (string filter in filters)
            {
                ofn.lpstrFilter += $"\0*.{filter}";
            }

            ofn.lpstrFile = new string(new char[256]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFileTitle = new string(new char[64]);
            ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
            ofn.lpstrTitle = dialogTitle;

            if (GetOpenFileName(ref ofn))
            {
                return ofn.lpstrFile;
            }

            return string.Empty;
        }
    }
}
