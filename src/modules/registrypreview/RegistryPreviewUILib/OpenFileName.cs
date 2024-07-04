// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace RegistryPreviewUILib
{
    // Workaround for File Pickers that don't work while running as admin, per:
    // https://github.com/microsoft/WindowsAppSDK/issues/2504
    public static partial class OpenFilePicker
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref FileName openFileName);

        public static string ShowDialog(IntPtr windowHandle, string filter, string dialogTitle)
        {
            FileName openFileName = default(FileName);
            openFileName.StructSize = Marshal.SizeOf(openFileName);

            openFileName.HwndOwner = windowHandle;
            openFileName.Filter = filter;
            openFileName.File = new string(new char[256]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[64]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.Title = dialogTitle;

            if (GetOpenFileName(ref openFileName))
            {
                return openFileName.File;
            }

            return string.Empty;
        }
    }
}
