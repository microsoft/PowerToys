// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class EmlMimeParser
    {
        public static EmlMimePart Parse(byte[] data)
        {
            int separator = FindHeaderSeparator(data, out int separatorLength);
            if (separator < 0)
            {
                throw new InvalidDataException("The email does not contain a valid header section.");
            }

            string headerText = Encoding.Latin1.GetString(data, 0, separator);
            Dictionary<string, string> headers = ParseHeaders(headerText);
            byte[] body = data[(separator + separatorLength)..];
            ParseContentType(GetHeader(headers, "Content-Type"), out string contentType, out Dictionary<string, string> parameters);
            EmlMimePart part = new(headers, contentType, parameters, body);

            if (contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) && parameters.TryGetValue("boundary", out string? boundary))
            {
                foreach (byte[] child in SplitMultipart(body, boundary))
                {
                    part.Children.Add(Parse(child));
                }
            }

            return part;
        }

        public static EmlMimePart? FindBody(EmlMimePart part, string contentType)
        {
            if (part.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase) && !GetHeader(part.Headers, "Content-Disposition").StartsWith("attachment", StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }

            foreach (EmlMimePart child in part.Children)
            {
                EmlMimePart? match = FindBody(child, contentType);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        public static string GetHeader(Dictionary<string, string> headers, string name) => headers.TryGetValue(name, out string? value) ? value : string.Empty;

        private static Dictionary<string, string> ParseHeaders(string value)
        {
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            string unfolded = Regex.Replace(value, @"\r?\n[\t ]+", " ");
            foreach (string line in unfolded.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string name = line[..colon].Trim();
                    string headerValue = line[(colon + 1)..].Trim();
                    headers[name] = headers.TryGetValue(name, out string? existing) ? $"{existing}, {headerValue}" : headerValue;
                }
            }

            return headers;
        }

        private static void ParseContentType(string value, out string contentType, out Dictionary<string, string> parameters)
        {
            parameters = new(StringComparer.OrdinalIgnoreCase);
            string[] pieces = value.Split(';');
            contentType = string.IsNullOrWhiteSpace(pieces[0]) ? "text/plain" : pieces[0].Trim();
            foreach (string piece in pieces.Skip(1))
            {
                int equals = piece.IndexOf('=');
                if (equals > 0)
                {
                    parameters[piece[..equals].Trim()] = piece[(equals + 1)..].Trim().Trim('"');
                }
            }
        }

        private static IEnumerable<byte[]> SplitMultipart(byte[] data, string boundary)
        {
            string body = Encoding.Latin1.GetString(data);
            string marker = "--" + boundary;
            foreach (string section in body.Split(marker, StringSplitOptions.None).Skip(1))
            {
                if (section.StartsWith("--", StringComparison.Ordinal))
                {
                    yield break;
                }

                string trimmed = section.TrimStart('\r', '\n').TrimEnd('\r', '\n');
                if (trimmed.Length > 0)
                {
                    yield return Encoding.Latin1.GetBytes(trimmed);
                }
            }
        }

        private static int FindHeaderSeparator(byte[] data, out int length)
        {
            for (int i = 0; i < data.Length - 1; i++)
            {
                if (i + 3 < data.Length && data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                {
                    length = 4;
                    return i;
                }

                if (data[i] == '\n' && data[i + 1] == '\n')
                {
                    length = 2;
                    return i;
                }
            }

            length = 0;
            return -1;
        }
    }
}
