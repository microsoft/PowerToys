// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class EmailHtmlRenderer
    {
        public static string Render(EmailMessage message)
        {
            StringBuilder headers = new();
            AddHeader(headers, "From", message.Sender);
            AddHeader(headers, "To", string.Join("; ", message.To));
            AddHeader(headers, "Cc", string.Join("; ", message.Cc));
            AddHeader(headers, "Bcc", string.Join("; ", message.Bcc));
            AddHeader(headers, "Subject", message.Subject);
            AddHeader(headers, "Date", message.Date?.ToString("f", CultureInfo.CurrentCulture) ?? string.Empty);

            string body = message.IsBodyHtml ? ResolveInlineImages(SanitizeHtml(message.Body), message) : $"<pre>{WebUtility.HtmlEncode(message.Body)}</pre>";
            return $$"""
                <!doctype html>
                <html><head><meta charset="utf-8"><meta name="color-scheme" content="light dark">
                <style>
                :root { font-family: "Segoe UI", sans-serif; color-scheme: light dark; }
                body { margin: 0; padding: 24px; overflow-wrap: anywhere; }
                .headers { display: grid; grid-template-columns: max-content 1fr; gap: 7px 14px; padding-bottom: 18px; border-bottom: 1px solid GrayText; }
                .label { color: GrayText; font-weight: 600; }
                .body { margin-top: 20px; }
                pre { margin: 0; white-space: pre-wrap; font: inherit; }
                img { max-width: 100%; height: auto; }
                </style></head><body><section class="headers">{{headers}}</section><main class="body">{{body}}</main></body></html>
                """;
        }

        private static string ResolveInlineImages(string html, EmailMessage message)
        {
            if (message.InlineImages.Count == 0)
            {
                return html;
            }

            return Regex.Replace(
                html,
                @"cid:([^\s'""<>\)]+)",
                match =>
                {
                    string contentId = Uri.UnescapeDataString(match.Groups[1].Value).Trim('<', '>');
                    return message.InlineImages.TryGetValue(contentId, out EmailInlineImage? image)
                        ? $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Data)}"
                        : match.Value;
                },
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1));
        }

        private static void AddHeader(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append("<div class=\"label\">").Append(label).Append("</div><div>")
                    .Append(WebUtility.HtmlEncode(value)).Append("</div>");
            }
        }

        private static string SanitizeHtml(string html)
        {
            // WebView2 scripting is disabled and its resource filter blocks remote content. Remove
            // active/embedded elements as an additional defense before placing message HTML in the page.
            string[] elements = ["script", "iframe", "object", "embed", "frame", "frameset", "base"];
            foreach (string element in elements)
            {
                html = Regex.Replace(
                    html,
                    $@"<{element}\b[^>]*>.*?</{element}\s*>",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline,
                    TimeSpan.FromSeconds(1));
                html = Regex.Replace(
                    html,
                    $@"<{element}\b[^>]*/?>",
                    string.Empty,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1));
            }

            return Regex.Replace(
                html,
                @"\s+on[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)",
                string.Empty,
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1));
        }
    }
}
