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
        private const int MaxDigitsToDisplay = 3;
        private const int PowerFactor = 1024;

        public static string BytesToReadableString(ulong bytes, bool showTotalBytes = true)
        {
            string totalBytesDisplays = (bytes == 1) ?
                ResourceLoaderInstance.ResourceLoader.GetString("ReadableString_ByteString") :
                ResourceLoaderInstance.ResourceLoader.GetString("ReadableString_BytesString");

            int index = 0;
            double number = 0.0;

            if (bytes > 0)
            {
                index = (int)Math.Floor(Math.Log(bytes) / Math.Log(PowerFactor));
                number = bytes / Math.Pow(PowerFactor, index);
            }

            if (index > 0 && number >= Math.Pow(10, MaxDigitsToDisplay))
            {
                index++;
                number = bytes / Math.Pow(PowerFactor, index);
            }

            int precision = GetPrecision(index, number);
            int decimalPrecision = (int)Math.Pow(10, precision);

            number = Math.Truncate(number * decimalPrecision) / decimalPrecision;

            string formatSpecifier = GetFormatSpecifierString(index, number, bytes, precision);

            return bytes == 0 || !showTotalBytes
                ? string.Format(CultureInfo.CurrentCulture, formatSpecifier, number)
                : string.Format(CultureInfo.CurrentCulture, formatSpecifier + totalBytesDisplays, number, bytes);
        }

        public static string FormatResourceString(string resourceId, object? args)
        {
            var formatString = ResourceLoaderInstance.ResourceLoader.GetString(resourceId);
            var formattedString = string.IsNullOrEmpty(formatString) ? string.Empty : string.Format(CultureInfo.InvariantCulture, formatString, args);

            return formattedString;
        }

        public static string FormatResourceString(string resourceId, object? args0, object? args1)
        {
            var formatString = ResourceLoaderInstance.ResourceLoader.GetString(resourceId);
            var formattedString = string.IsNullOrEmpty(formatString) ? string.Empty : string.Format(CultureInfo.InvariantCulture, formatString, args0, args1);

            return formattedString;
        }

        public static int GetPrecision(int index, double number)
        {
            int numberOfDigits = MathHelper.NumberOfDigits((int)number);
            return index == 0 ?
                0 :
                MaxDigitsToDisplay - numberOfDigits;
        }

        public static string GetFormatSpecifierString(int index, double number, ulong bytes, int precision)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            List<string> format = new List<string>
            {
                (bytes == 1) ?
                    resourceLoader.GetString("ReadableString_ByteAbbreviationFormat") : // "byte"
                    resourceLoader.GetString("ReadableString_BytesAbbreviationFormat"), // "bytes"
                resourceLoader.GetString("ReadableString_KiloByteAbbreviationFormat"), // "KB"
                resourceLoader.GetString("ReadableString_MegaByteAbbreviationFormat"), // "MB"
                resourceLoader.GetString("ReadableString_GigaByteAbbreviationFormat"), // "GB"
                resourceLoader.GetString("ReadableString_TeraByteAbbreviationFormat"), // "TB"
                resourceLoader.GetString("ReadableString_PetaByteAbbreviationFormat"), // "PB"
                resourceLoader.GetString("ReadableString_ExaByteAbbreviationFormat"),  // "EB"
            };

            return "{0:F" + precision + "} " + format[index];
        }
    }
}
