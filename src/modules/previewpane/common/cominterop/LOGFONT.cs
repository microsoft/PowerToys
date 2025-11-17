// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Defines the attributes of a font.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct LOGFONT
    {
        /// <summary>
        /// Gets or sets value of type INT that specifies the height, in logical units, of the font's character cell or character.
        /// </summary>
        public int LfHeight { get; set; }

        /// <summary>
        /// Gets or sets value of type INT that specifies the width, in logical units, of characters in the font.
        /// </summary>
        public int LfWidth { get; set; }

        /// <summary>
        /// Gets or sets value of type INT that contains the angle, in tenths of degrees, between the escapement vector and the x-axis of the device. The escapement
        /// vector is parallel to the base line of a row of text.
        /// </summary>
        public int LfEscapement { get; set; }

        /// <summary>
        /// Gets or sets value of type INT that specifies the angle, in tenths of degrees, between each character's base line and the x-axis of the device.
        /// </summary>
        public int LfOrientation { get; set; }

        /// <summary>
        /// Gets or sets value of type INT that specifies the weight of the font in the range from 0 through 1000.
        /// </summary>
        public int LfWeight { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies an italic font if set to TRUE.
        /// </summary>
        public byte LfItalic { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies an underlined font if set to TRUE.
        /// </summary>
        public byte LfUnderline { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies a strikeout font if set to TRUE.
        /// </summary>
        public byte LfStrikeOut { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies the character set.
        /// </summary>
        public byte LfCharSet { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies the output precision. The output precision defines how closely the output must match the requested
        /// font's height, width, character orientation, escapement, pitch, and font type.
        /// </summary>
        public byte LfOutPrecision { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies the clipping precision. The clipping precision defines how to clip characters that are partially outside the clipping region.
        /// </summary>
        public byte LfClipPrecision { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies the output quality. The output quality defines how carefully the GDI must attempt to match the logical-font attributes to those of an actual physical font.
        /// </summary>
        public byte LfQuality { get; set; }

        /// <summary>
        /// Gets or sets value of type BYTE that specifies the pitch and family of the font.
        /// </summary>
        public byte LfPitchAndFamily { get; set; }

        /// <summary>
        /// Gets or sets array of wide characters that contains a null-terminated string that specifies the typeface name of the font. The length of the string must not exceed 32 characters, including the NULL terminator.
        /// </summary>
        public string LfFaceName
        {
            get { return _lfFaceName; }
            set { _lfFaceName = value; }
        }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        private string _lfFaceName;
    }
}
