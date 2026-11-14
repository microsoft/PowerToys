// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class EmlContentDecoder
    {
        public static string DecodeBody(EmlMimePart part)
        {
            byte[] decoded = DecodeTransferEncoding(part.Body, EmlMimeParser.GetHeader(part.Headers, "Content-Transfer-Encoding"));
            string charset = part.Parameters.TryGetValue("charset", out string? value) ? value : "utf-8";
            try
            {
                return Encoding.GetEncoding(charset).GetString(decoded);
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8.GetString(decoded);
            }
        }

        public static byte[] DecodeTransferEncoding(byte[] data, string transferEncoding)
        {
            if (transferEncoding.Trim().Equals("base64", StringComparison.OrdinalIgnoreCase))
            {
                string value = Encoding.ASCII.GetString(data);
                return Convert.FromBase64String(Regex.Replace(value, @"\s", string.Empty));
            }

            if (!transferEncoding.Trim().Equals("quoted-printable", StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }

            using MemoryStream result = new();
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == '=' && i + 1 < data.Length)
                {
                    if (data[i + 1] == '\r' && i + 2 < data.Length && data[i + 2] == '\n')
                    {
                        i += 2;
                        continue;
                    }

                    if (data[i + 1] == '\n')
                    {
                        i++;
                        continue;
                    }

                    if (i + 2 < data.Length && TryHex(data[i + 1], out int high) && TryHex(data[i + 2], out int low))
                    {
                        result.WriteByte((byte)((high << 4) | low));
                        i += 2;
                        continue;
                    }
                }

                result.WriteByte(data[i]);
            }

            return result.ToArray();
        }

        public static string DecodeHeader(string value)
        {
            return Regex.Replace(value, @"=\?([^?]+)\?([bBqQ])\?([^?]*)\?=", match =>
            {
                try
                {
                    Encoding encoding = Encoding.GetEncoding(match.Groups[1].Value);
                    byte[] bytes = match.Groups[2].Value.Equals("B", StringComparison.OrdinalIgnoreCase)
                        ? Convert.FromBase64String(match.Groups[3].Value)
                        : DecodeTransferEncoding(Encoding.ASCII.GetBytes(match.Groups[3].Value.Replace('_', ' ')), "quoted-printable");
                    return encoding.GetString(bytes);
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException)
                {
                    return match.Value;
                }
            });
        }

        private static bool TryHex(byte value, out int result)
        {
            result = value >= '0' && value <= '9' ? value - '0' : value >= 'A' && value <= 'F' ? value - 'A' + 10 : value >= 'a' && value <= 'f' ? value - 'a' + 10 : -1;
            return result >= 0;
        }
    }
}
