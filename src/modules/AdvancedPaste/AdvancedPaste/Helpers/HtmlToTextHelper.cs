// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using HtmlAgilityPack;

namespace AdvancedPaste.Helpers;

/// <summary>
/// Managed HTML-to-plain-text conversion for clipboard content, without native rendering.
/// Windows.Data.Html.HtmlUtilities.ConvertToText can fail-fast after WinUI startup.
/// </summary>
internal static class HtmlToTextHelper
{
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "dd", "details", "div", "dl", "dt",
        "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4",
        "h5", "h6", "header", "hr", "li", "main", "nav", "ol", "p", "pre", "section",
        "table", "tr", "ul",
    };

    /// <summary>Extracts text from an HTML fragment while retaining block boundaries and preformatted whitespace.</summary>
    internal static string ToPlainText(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var text = new StringBuilder();
        var nodes = new Stack<(HtmlNode Node, bool Closing, bool Preformatted)>();
        nodes.Push((document.DocumentNode, false, false));
        var pendingSpace = false;

        // Only real text-node characters advance this boundary, never generated separators.
        // Final truncation and table-row tab trimming both depend on that distinction.
        var contentLength = 0;

        // Walk iteratively so deeply nested clipboard markup cannot overflow our call stack.
        while (nodes.TryPop(out var entry))
        {
            var node = entry.Node;
            if (node.NodeType == HtmlNodeType.Text)
            {
                var value = WebUtility.HtmlDecode(((HtmlTextNode)node).Text);
                foreach (var character in value)
                {
                    // Collapse HTML's ASCII whitespace only; preserve typographic and nonbreaking Unicode spaces.
                    if (!entry.Preformatted && character is ' ' or '\t' or '\r' or '\n' or '\f')
                    {
                        pendingSpace = true;
                        continue;
                    }

                    if (pendingSpace && text.Length > 0 && text[^1] is not ('\r' or '\n' or '\t'))
                    {
                        text.Append(' ');
                    }

                    pendingSpace = false;
                    text.Append(character);
                    contentLength = text.Length;
                }

                continue;
            }

            if (node.NodeType is not (HtmlNodeType.Element or HtmlNodeType.Document) ||
                node.Name is "head" or "script" or "style" or "template" ||
                node.Attributes.Contains("hidden"))
            {
                continue;
            }

            if (entry.Closing)
            {
                pendingSpace = false;
                if (node.Name is "td" or "th")
                {
                    text.Append('\t');
                }
                else
                {
                    if (node.Name == "tr")
                    {
                        while (text.Length > contentLength && text[^1] == '\t')
                        {
                            text.Length--;
                        }
                    }

                    AppendLineBreak(text);
                }

                continue;
            }

            if (node.Name == "br")
            {
                text.Append('\n');
                pendingSpace = false;
                continue;
            }

            var block = BlockElements.Contains(node.Name);
            if (block)
            {
                AppendLineBreak(text);
                pendingSpace = false;
            }

            if (block || node.Name is "td" or "th")
            {
                nodes.Push((node, true, entry.Preformatted));
            }

            var preformatted = entry.Preformatted || node.Name == "pre";
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                nodes.Push((child, false, preformatted));
            }
        }

        // Discard only generated trailing separators, not whitespace belonging to preformatted text.
        text.Length = contentLength;
        return text.ToString().ReplaceLineEndings(Environment.NewLine);
    }

    private static void AppendLineBreak(StringBuilder text)
    {
        if (text.Length > 0 && text[^1] is not ('\r' or '\n'))
        {
            text.Append('\n');
        }
    }
}
