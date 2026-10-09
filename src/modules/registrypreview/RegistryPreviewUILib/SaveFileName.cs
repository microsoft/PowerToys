// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace RegistryPreviewUILib
{
    // Workaround for File Pickers that don't work while running as admin, per:
    // https://github.com/microsoft/WindowsAppSDK/issues/2504
    public static partial class SaveFilePicker
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetSaveFileName(ref FileName saveFileName);

        public static string ShowDialog(IntPtr windowHandle, string suggestedFilename, string filter, string dialogTitle)
        {
            FileName saveFileName = default(FileName);
            saveFileName.StructSize = Marshal.SizeOf(saveFileName);

            saveFileName.HwndOwner = windowHandle;
            saveFileName.Filter = filter;
            saveFileName.File = new string(new char[256]);
            saveFileName.MaxFile = saveFileName.File.Length;
            saveFileName.File = string.Concat(suggestedFilename, saveFileName.File);
            saveFileName.FileTitle = new string(new char[64]);
            saveFileName.MaxFileTitle = saveFileName.FileTitle.Length;
            saveFileName.Title = dialogTitle;
            saveFileName.DefExt = "reg";

            if (GetSaveFileName(ref saveFileName))
            {
                return saveFileName.File;
            }

            return string.Empty;
        }
    }
}
