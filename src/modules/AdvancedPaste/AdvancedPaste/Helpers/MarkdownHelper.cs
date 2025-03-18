﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HtmlAgilityPack;
using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers
{
    internal static class MarkdownHelper
    {
        public static async Task<string> ToMarkdownAsync(DataPackageView clipboardData)
        {
            Logger.LogTrace();

            var data = clipboardData.Contains(StandardDataFormats.Html) ? await clipboardData.GetHtmlFormatAsync()
                     : clipboardData.Contains(StandardDataFormats.Text) ? await clipboardData.GetTextAsync()
                     : string.Empty;

            return string.IsNullOrEmpty(data) ? string.Empty : ConvertHtmlToMarkdown(CleanHtml(data));
        }

        private static string CleanHtml(string html)
        {
            Logger.LogTrace();

            // Remove the "StartFragment" and "EndFragment" comments
            html = Regex.Replace(html, @"<!--StartFragment-->|<!--EndFragment-->", string.Empty);

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(html);

            // Remove unwanted HTML elements
            RemoveUnwantedElements(document.DocumentNode);

            // Remove inline styles
            RemoveInlineStyles(document.DocumentNode);

            // Clean up line breaks and whitespace
            CleanUpWhitespace(document.DocumentNode);

            // Serialize the cleaned HTML back to string
            using (var writer = new System.IO.StringWriter())
            {
                document.Save(writer);
                return writer.ToString();
            }
        }

        private static void RemoveUnwantedElements(HtmlNode node)
        {
            Logger.LogTrace();

            // Remove specific elements by tag name, CSS class, or other attributes
            // Example: Remove all <script> elements
            foreach (var scriptNode in node.DescendantsAndSelf("script").ToArray())
            {
                scriptNode.Remove();
            }

            // Ignore specific elements like <sup> elements
            foreach (var ignoredElement in node.DescendantsAndSelf("sup").ToArray())
            {
                ignoredElement.Remove();
            }

            // Ignore specific elements like <o:p> element added by MS Word
            foreach (var ignoredElement in node.DescendantsAndSelf("o:p").ToArray())
            {
                ignoredElement.Remove();
            }
        }

        private static void RemoveInlineStyles(HtmlNode node)
        {
            Logger.LogTrace();

            // Remove inline styles from elements
            foreach (var elementNode in node.DescendantsAndSelf())
            {
                elementNode.Attributes.Remove("style");
            }
        }

        private static void CleanUpWhitespace(HtmlNode node)
        {
            Logger.LogTrace();

            // Clean up line breaks and excessive whitespace
            if (node.NodeType == HtmlNodeType.Text)
            {
                node.InnerHtml = Regex.Replace(node.InnerHtml, @"\s{2,}", " ");
                node.InnerHtml = Regex.Replace(node.InnerHtml, @"[\r\n]+", string.Empty);
            }
            else
            {
                foreach (var childNode in node.ChildNodes.ToArray())
                {
                    CleanUpWhitespace(childNode);
                }
            }
        }

        private static string ConvertHtmlToMarkdown(string html)
        {
            Logger.LogTrace();

            // Perform the conversion from HTML to Markdown using your chosen library or method
            var converter = new ReverseMarkdown.Converter();
            string markdown = converter.Convert(html);
            return markdown;
        }
    }
}
