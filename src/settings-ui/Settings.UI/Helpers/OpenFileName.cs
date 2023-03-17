// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed.")]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class OpenFileName
    {
        public int StructSize;
        public IntPtr Hwnd = IntPtr.Zero;
        public IntPtr Hinst = IntPtr.Zero;
        public string Filter;
        public string CustFilter;
        public int CustFilterMax;
        public int FilterIndex;
        public string File;
        public int MaxFile;
        public string FileTitle;
        public int MaxFileTitle;
        public string InitialDir;
        public string Title;
        public int Flags;
        public short FileOffset;
        public short FileExtMax;
        public string DefExt;
        public int CustData;
        public IntPtr Hook = IntPtr.Zero;
        public string Template;
    }
}
