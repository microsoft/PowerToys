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
        private const int DecimalPercision = 10;

        public static string BytesToReadableString(ulong bytes)
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

            int index = 0;
            double number = 0.0;

            if (bytes > 0)
            {
                index = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
                number = Math.Round((bytes / Math.Pow(1024, index)) * DecimalPercision) / DecimalPercision;
            }

            return string.Format(CultureInfo.InvariantCulture, format[index], number);
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
    }
}
