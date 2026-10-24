// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Reads and writes the snippet file in the encoding it already has. The engine reads it with
    /// <c>StreamReader</c>'s byte order mark detection, so Settings recognizes the same marks:
    /// UTF-8, UTF-16 and UTF-32, little or big endian. A file without one is UTF-8, as in the engine.
    /// Saving keeps the encoding and its mark, so a UTF-16 file stays UTF-16.
    /// </summary>
    internal static class TextExpanderSnippetEncoding
    {
        /// <summary>Gets the encoding <paramref name="bytes"/> are in, and how many bytes its byte order mark takes.</summary>
        public static Encoding Detect(ReadOnlySpan<byte> bytes, out int markLength)
        {
            // The order matters: a UTF-32 little-endian mark starts with the UTF-16 one.
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
            {
                markLength = 4;
                return new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: false);
            }

            if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                markLength = 4;
                return new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: false);
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                markLength = 3;
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                markLength = 2;
                return new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: false);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                markLength = 2;
                return new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: false);
            }

            markLength = 0;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        }

        /// <summary>Decodes a snippet file's bytes, and returns the encoding to save it with.</summary>
        public static string Decode(byte[] bytes, out Encoding encoding)
        {
            encoding = Detect(bytes, out int markLength);
            return encoding.GetString(bytes, markLength, bytes.Length - markLength);
        }

        /// <summary>Encodes <paramref name="text"/> with the encoding's byte order mark, if it has one, in one array.</summary>
        public static byte[] Encode(string text, Encoding encoding)
        {
            byte[] mark = encoding.GetPreamble();
            byte[] bytes = new byte[mark.Length + encoding.GetByteCount(text)];
            mark.CopyTo(bytes, 0);
            encoding.GetBytes(text, 0, text.Length, bytes, mark.Length);
            return bytes;
        }
    }
}
