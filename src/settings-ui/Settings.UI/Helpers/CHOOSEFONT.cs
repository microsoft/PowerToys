// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Keep original names from original structure")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = "Keep original names from original structure")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Structure used for win32 interop. We need to access the fields")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "Let's have initializations be explicit for these win32 interop types")]

    // Class to select the Dialog options to call ChooseFont.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class CHOOSEFONT
    {
        public int lStructSize = Marshal.SizeOf(typeof(CHOOSEFONT));
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr hDC = IntPtr.Zero;
        public IntPtr lpLogFont = IntPtr.Zero;
        public int iPointSize = 0;
        public int Flags = 0;
        public int rgbColors = 0;
        public IntPtr lCustData = IntPtr.Zero;
        public IntPtr lpfnHook = IntPtr.Zero;
        public string lpTemplateName = null;
        public IntPtr hInstance = IntPtr.Zero;
        public string lpszStyle = null;
        public short nFontType;
        private short __MISSING_ALIGNMENT__;
        public int nSizeMin;
        public int nSizeMax;
    }

    [Flags]
    public enum CHOOSE_FONT_FLAGS
    {
        CF_SCREENFONTS = 0x00000001,
        CF_PRINTER_FONTS = 0x00000002,
        CF_BOTH = CF_SCREENFONTS | CF_PRINTER_FONTS,
        CF_SHOW_HELP = 0x00000004,
        CF_ENABLE_HOOK = 0x00000008,
        CF_ENABLETEMPLATE = 0x00000010,
        CF_ENABLETEMPLATE_HANDLE = 0x00000020,
        CF_INITTOLOGFONTSTRUCT = 0x00000040,
        CF_USE_STYLE = 0x00000080,
        CF_EFFECTS = 0x00000100,
        CF_APPLY = 0x00000200,
        CF_ANSI_ONLY = 0x00000400,
        CF_SCRIPTS_ONLY = CF_ANSI_ONLY,
        CF_NO_VECTOR_FONTS = 0x00000800,
        CF_NO_OEM_FONTS = CF_NO_VECTOR_FONTS,
        CF_NO_SIMULATIONS = 0x00001000,
        CF_LIMITSIZE = 0x00002000,
        CF_FIXED_PITCH_ONLY = 0x00004000,
        CF_WYSIWYG = 0x00008000,
        CF_FORCE_FONT_EXIST = 0x00010000,
        CF_SCALABLE_ONLY = 0x00020000,
        CF_TT_ONLY = 0x00040000,
        CF_NO_FACE_SEL = 0x00080000,
        CF_NO_STYLE_SEL = 0x00100000,
        CF_NO_SIZE_SEL = 0x00200000,
        CF_SELECT_SCRIPT = 0x00400000,
        CF_NO_SCRIPT_SEL = 0x00800000,
        CF_NO_VERT_FONTS = 0x01000000,
        CF_INACTIVE_FONTS = 0x02000000,
    }
}
