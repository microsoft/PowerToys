// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;

namespace AdvancedPaste.Core;

public static class MarkdownConverter
{
    public static string Convert(string html, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        html = Regex.Replace(html, @"<!--StartFragment-->|<!--EndFragment-->", string.Empty);

        var document = new HtmlDocument();
        cancellationToken.ThrowIfCancellationRequested();
        document.LoadHtml(html);
        cancellationToken.ThrowIfCancellationRequested();

        CleanNodes(document.DocumentNode, cancellationToken);

        using var writer = new System.IO.StringWriter();
        document.Save(writer);
        var markdown = new ReverseMarkdown.Converter().Convert(writer.ToString());
        cancellationToken.ThrowIfCancellationRequested();
        return markdown;
    }

    private static void CleanNodes(HtmlNode root, CancellationToken cancellationToken)
    {
        Stack<HtmlNode> nodes = new();
        nodes.Push(root);
        while (nodes.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = nodes.Pop();

            if (node.NodeType == HtmlNodeType.Element &&
                (node.Name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                 node.Name.Equals("sup", StringComparison.OrdinalIgnoreCase) ||
                 node.Name.Equals("o:p", StringComparison.OrdinalIgnoreCase)))
            {
                node.Remove();
                continue;
            }

            if (node.NodeType == HtmlNodeType.Text)
            {
                node.InnerHtml = Regex.Replace(node.InnerHtml, @"\s{2,}", " ");
                node.InnerHtml = Regex.Replace(node.InnerHtml, @"[\r\n]+", string.Empty);
                continue;
            }

            node.Attributes.Remove("style");
            for (var i = node.ChildNodes.Count - 1; i >= 0; i--)
            {
                nodes.Push(node.ChildNodes[i]);
            }
        }
    }
}
