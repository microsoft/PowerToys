// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Keep original names from original structure")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Structure used for win32 interop. We need to access the fields")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "Let's have initializations be explicit for these win32 interop types")]

    // Result from calling ChooseFont.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class LOGFONT
    {
        public int lfHeight = 0;
        public int lfWidth = 0;
        public int lfEscapement = 0;
        public int lfOrientation = 0;
        public int lfWeight = 0;
        public byte lfItalic = 0;
        public byte lfUnderline = 0;
        public byte lfStrikeOut = 0;
        public byte lfCharSet = 0;
        public byte lfOutPrecision = 0;
        public byte lfClipPrecision = 0;
        public byte lfQuality = 0;
        public byte lfPitchAndFamily = 0;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName = string.Empty;
    }

    public enum FontWeight : int
    {
        FW_DONT_CARE = 0,
        FW_THIN = 100,
        FW_EXTRALIGHT = 200,
        FW_LIGHT = 300,
        FW_NORMAL = 400,
        FW_MEDIUM = 500,
        FW_SEMIBOLD = 600,
        FW_BOLD = 700,
        FW_EXTRABOLD = 800,
        FW_HEAVY = 900,
    }

    public enum FontCharSet : byte
    {
        ANSI_CHARSET = 0,
        DEFAULT_CHARSET = 1,
        SYMBOL_CHARSET = 2,
        SHIFT_JIS_CHARSET = 128,
        HANGEUL_CHARSET = 129,
        HANGUL_CHARSET = HANGEUL_CHARSET,
        GB2312_CHARSET = 134,
        CHINESE_BIG5_CHARSET = 136,
        OEM_CHARSET = 255,
        JOHAB_CHARSET = 130,
        HEBREW_CHARSET = 177,
        ARABIC_CHARSET = 178,
        GREEK_CHARSET = 161,
        TURKISH_CHARSET = 162,
        VIETNAMESE_CHARSET = 163,
        THAI_CHARSET = 222,
        EAST_EUROPE_CHARSET = 238,
        RUSSIAN_CHARSET = 204,
        MAC_CHARSET = 77,
        BALTIC_CHARSET = 186,
    }

    public enum FontPrecision : byte
    {
        OUT_DEFAULT_PRECIS = 0,
        OUT_STRING_PRECIS = 1,
        OUT_CHARACTER_PRECIS = 2,
        OUT_STROKE_PRECIS = 3,
        OUT_TT_PRECIS = 4,
        OUT_DEVICE_PRECIS = 5,
        OUT_RASTER_PRECIS = 6,
        OUT_TT_ONLY_PRECIS = 7,
        OUT_OUTLINE_PRECIS = 8,
        OUT_SCREEN_OUTLINE_PRECIS = 9,
        OUT_PS_ONLY_PRECIS = 10,
    }

    public enum FontClipPrecision : byte
    {
        CLIP_DEFAULT_PRECIS = 0,
        CLIP_CHARACTER_PRECIS = 1,
        CLIP_STROKE_PRECIS = 2,
        CLIP_MASK = 0xf,
        CLIP_LH_ANGLES = 1 << 4,
        CLIP_TT_ALWAYS = 2 << 4,
        CLIP_DFA_DISABLE = 4 << 4,
        CLIP_EMBEDDED = 8 << 4,
    }

    public enum FontQuality : byte
    {
        DEFAULT_QUALITY = 0,
        DRAFT_QUALITY = 1,
        PROOF_QUALITY = 2,
        NONANTIALIASED_QUALITY = 3,
        ANTIALIASED_QUALITY = 4,
        CLEAR_TYPE_QUALITY = 5,
        CLEAR_TYPE_NATURAL_QUALITY = 6,
    }

    [Flags]
    public enum FontPitchAndFamily : byte
    {
        DEFAULT_PITCH = 0,
        FIXED_PITCH = 1,
        VARIABLE_PITCH = 2,
        FF_DONT_CARE = DEFAULT_PITCH,
        FF_ROMAN = 1 << 4,
        FF_SWISS = 2 << 4,
        FF_MODERN = 3 << 4,
        FF_SCRIPT = 4 << 4,
        FF_DECORATIVE = 5 << 4,
    }
}
