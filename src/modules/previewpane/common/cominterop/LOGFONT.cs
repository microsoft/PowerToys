// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct LOGFONT
    {
        public int LfHeight;
        public int LfWidth;
        public int LfEscapement;
        public int LfOrientation;
        public int LfWeight;
        public byte LfItalic;
        public byte LfUnderline;
        public byte LfStrikeOut;
        public byte LfCharSet;
        public byte LfOutPrecision;
        public byte LfClipPrecision;
        public byte LfQuality;
        public byte LfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string LfFaceName;
    }
}
