// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class EmlReader
    {
        public static EmailMessage Read(string path)
        {
            EmlMimePart root = EmlMimeParser.Parse(File.ReadAllBytes(path));
            EmailMessage message = new()
            {
                Sender = EmlContentDecoder.DecodeHeader(EmlMimeParser.GetHeader(root.Headers, "From")),
                Subject = EmlContentDecoder.DecodeHeader(EmlMimeParser.GetHeader(root.Headers, "Subject")),
            };

            AddAddresses(message.To, EmlMimeParser.GetHeader(root.Headers, "To"));
            AddAddresses(message.Cc, EmlMimeParser.GetHeader(root.Headers, "Cc"));
            AddAddresses(message.Bcc, EmlMimeParser.GetHeader(root.Headers, "Bcc"));
            if (DateTimeOffset.TryParse(EmlMimeParser.GetHeader(root.Headers, "Date"), out DateTimeOffset date))
            {
                message.Date = date;
            }

            EmlMimePart? body = EmlMimeParser.FindBody(root, "text/html") ?? EmlMimeParser.FindBody(root, "text/plain");
            if (body != null)
            {
                message.Body = EmlContentDecoder.DecodeBody(body);
                message.IsBodyHtml = body.ContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase);
            }

            AddInlineImages(root, message);

            return message;
        }

        private static void AddInlineImages(EmlMimePart part, EmailMessage message)
        {
            string contentId = EmlMimeParser.GetHeader(part.Headers, "Content-ID").Trim().Trim('<', '>');
            if (part.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(contentId))
            {
                byte[] data = EmlContentDecoder.DecodeTransferEncoding(part.Body, EmlMimeParser.GetHeader(part.Headers, "Content-Transfer-Encoding"));
                if (data.Length > 0)
                {
                    message.InlineImages[contentId] = new EmailInlineImage(part.ContentType, data);
                }
            }

            foreach (EmlMimePart child in part.Children)
            {
                AddInlineImages(child, message);
            }
        }

        private static void AddAddresses(List<string> destination, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                destination.AddRange(value.Split(',').Select(address => EmlContentDecoder.DecodeHeader(address.Trim())).Where(address => address.Length > 0));
            }
        }
    }
}
