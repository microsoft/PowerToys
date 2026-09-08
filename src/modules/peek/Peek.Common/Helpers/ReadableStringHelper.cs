// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Peek.Common.Helpers
{
    public static class ReadableStringHelper
    {
        /// <summary>
        /// The number of significant digits to show in the abbreviated size, spread
        /// across the whole number rather than just the decimals. The integer part is
        /// filled first and any remaining budget becomes decimal places, so the mantissa
        /// renders as e.g. "1.23 GB", "12.3 GB", or "123 GB". This also caps the mantissa
        /// below 1000 (see the unit-promotion guard in <see
        /// cref="BytesToReadableString"/>), which keeps the integer part at three digits
        /// or fewer and avoids any culture-specific thousands grouping on the abbreviated
        /// value.
        /// </summary>
        private const int SignificantDigits = 3;
        private const int PowerFactor = 1024;

        /// <summary>
        /// Converts a byte count into a localized, human-readable size string, e.g.
        /// "1.5 MB". Uses <see cref="CultureInfo.CurrentCulture"/> because the result is
        /// user-facing display text.
        /// </summary>
        /// <param name="bytes">The size in bytes.</param>
        /// <param name="showTotalBytes">Whether to append the exact byte count in
        /// parentheses after the abbreviated size.</param>
        /// <returns>The localized, human-readable size string.</returns>
        public static string BytesToReadableString(ulong bytes, bool showTotalBytes = true)
        {
            string totalBytesDisplays = (bytes == 1) ?
                ResourceLoaderInstance.GetString("ReadableString_ByteString") :
                ResourceLoaderInstance.GetString("ReadableString_BytesString");

            int index = 0;
            double number = 0.0;

            if (bytes > 0)
            {
                index = (int)Math.Floor(Math.Log(bytes) / Math.Log(PowerFactor));
                number = bytes / Math.Pow(PowerFactor, index);
            }

            if (index > 0 && number >= Math.Pow(10, SignificantDigits))
            {
                index++;
                number = bytes / Math.Pow(PowerFactor, index);
            }

            int precision = GetPrecision(index, number);
            int decimalPrecision = (int)Math.Pow(10, precision);

            number = Math.Truncate(number * decimalPrecision) / decimalPrecision;

            string formatSpecifier = GetFormatSpecifierString(index, bytes, precision);

            return bytes == 0 || !showTotalBytes
                ? string.Format(CultureInfo.CurrentCulture, formatSpecifier, number)
                : string.Format(CultureInfo.CurrentCulture, formatSpecifier + totalBytesDisplays, number, bytes);
        }

        public static string FormatFolderContents(ulong files, ulong directories, bool isScanning, bool isPartial)
        {
            if (isScanning && files == 0 && directories == 0)
            {
                return ResourceLoaderInstance.GetString("UnsupportedFile_FolderContains_Scanning");
            }

            string formattedFiles = (files == 1)
                ? ResourceLoaderInstance.GetString("UnsupportedFile_FolderFileCount_Single")
                : ResourceLoaderInstance.FormatString("UnsupportedFile_FolderFileCount_Plural", files);

            string formattedDirectories = (directories == 1)
                ? ResourceLoaderInstance.GetString("UnsupportedFile_FolderDirectoryCount_Single")
                : ResourceLoaderInstance.FormatString("UnsupportedFile_FolderDirectoryCount_Plural", directories);

            string result = $"{formattedFiles}, {formattedDirectories}";

            if (isPartial)
            {
                string incomplete = ResourceLoaderInstance.GetString("UnsupportedFile_FolderContains_Incomplete");
                result += $" ({incomplete})";
            }

            return result;
        }

        private static int GetPrecision(int index, double number)
        {
            int numberOfDigits = MathHelper.NumberOfDigits((int)number);
            return index == 0
                ? 0
                : SignificantDigits - numberOfDigits;
        }

        private static string GetFormatSpecifierString(int index, ulong bytes, int precision)
        {
            List<string> format =
            [
                (bytes == 1)
                    ? ResourceLoaderInstance.GetString("ReadableString_ByteAbbreviationFormat") // "byte"
                    : ResourceLoaderInstance.GetString("ReadableString_BytesAbbreviationFormat"), // "bytes"
                      ResourceLoaderInstance.GetString("ReadableString_KiloByteAbbreviationFormat"), // "KB"
                      ResourceLoaderInstance.GetString("ReadableString_MegaByteAbbreviationFormat"), // "MB"
                      ResourceLoaderInstance.GetString("ReadableString_GigaByteAbbreviationFormat"), // "GB"
                      ResourceLoaderInstance.GetString("ReadableString_TeraByteAbbreviationFormat"), // "TB"
                      ResourceLoaderInstance.GetString("ReadableString_PetaByteAbbreviationFormat"), // "PB"
                      ResourceLoaderInstance.GetString("ReadableString_ExaByteAbbreviationFormat"),  // "EB"
            ];

            return "{0:F" + precision + "} " + format[index];
        }
    }
}
