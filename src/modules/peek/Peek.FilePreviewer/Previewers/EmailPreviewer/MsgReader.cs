// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class MsgReader
    {
        public static EmailMessage Read(string path)
        {
            using MsgStorage storage = MsgStorage.Open(path);
            string senderName = FirstNonEmpty(storage.ReadString("0C1A"), storage.ReadString("0042"));
            string senderAddress = FirstNonEmpty(storage.ReadString("0C1F"), storage.ReadString("0065"));
            EmailMessage message = new()
            {
                Subject = storage.ReadString("0037"),
                Sender = FormatAddress(senderName, senderAddress),
            };

            byte[]? html = storage.ReadStream("__substg1.0_10130102");
            if (html?.Length > 0)
            {
                message.Body = DecodeHtml(html);
                message.IsBodyHtml = true;
            }
            else
            {
                message.Body = storage.ReadString("1000");
            }

            byte[]? dateBytes = storage.ReadStream("__substg1.0_00390040");
            if (dateBytes?.Length >= sizeof(long))
            {
                try
                {
                    message.Date = DateTimeOffset.FromFileTime(BitConverter.ToInt64(dateBytes, 0));
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            MsgRecipientReader.Read(storage, message);
            return message;
        }

        public static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        public static string FormatAddress(string name, string address)
        {
            return string.IsNullOrWhiteSpace(name) ? address : string.IsNullOrWhiteSpace(address) || name.Contains(address, StringComparison.OrdinalIgnoreCase) ? name : $"{name} <{address}>";
        }

        private static string DecodeHtml(byte[] bytes)
        {
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || bytes[1] == 0))
            {
                return Encoding.Unicode.GetString(bytes).Trim('\0', '\uFEFF');
            }

            return Encoding.UTF8.GetString(bytes).Trim('\0', '\uFEFF');
        }
    }
}
