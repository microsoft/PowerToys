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
        public int StructSize = 0;
        public IntPtr Hwnd = IntPtr.Zero;
        public IntPtr Hinst = IntPtr.Zero;
        public string Filter = null;
        public string CustFilter = null;
        public int CustFilterMax = 0;
        public int FilterIndex = 0;
        public string File = null;
        public int MaxFile = 0;
        public string FileTitle = null;
        public int MaxFileTitle = 0;
        public string InitialDir = null;
        public string Title = null;
        public int Flags = 0;
        public short FileOffset = 0;
        public short FileExtMax = 0;
        public string DefExt = null;
        public int CustData = 0;
        public IntPtr Hook = IntPtr.Zero;
        public string Template = null;
    }
}
