// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace RegistryPreviewUILib
{
    // Workaround for File Pickers that don't work while running as admin, per:
    // https://github.com/microsoft/WindowsAppSDK/issues/2504
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct FileName
    {
        public int StructSize;
        public IntPtr HwndOwner;
        public IntPtr Instance;
        public string Filter;
        public string CustomFilter;
        public int MaxCustFilter;
        public int FilterIndex;
        public string File;
        public int MaxFile;
        public string FileTitle;
        public int MaxFileTitle;
        public string InitialDir;
        public string Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public string DefExt;
        public IntPtr CustData;
        public IntPtr Hook;
        public string TemplateName;
        public IntPtr PtrReserved;
        public int Reserved;
        public int FlagsEx;
    }
}
