// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
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
        document.LoadHtml(html);

        foreach (var node in document.DocumentNode.DescendantsAndSelf("script").ToArray())
        {
            node.Remove();
        }

        foreach (var node in document.DocumentNode.DescendantsAndSelf("sup").ToArray())
        {
            node.Remove();
        }

        foreach (var node in document.DocumentNode.DescendantsAndSelf("o:p").ToArray())
        {
            node.Remove();
        }

        CleanNode(document.DocumentNode, cancellationToken);

        using var writer = new System.IO.StringWriter();
        document.Save(writer);
        return new ReverseMarkdown.Converter().Convert(writer.ToString());
    }

    private static void CleanNode(HtmlNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.NodeType == HtmlNodeType.Text)
        {
            node.InnerHtml = Regex.Replace(node.InnerHtml, @"\s{2,}", " ");
            node.InnerHtml = Regex.Replace(node.InnerHtml, @"[\r\n]+", string.Empty);
            return;
        }

        foreach (var element in node.DescendantsAndSelf())
        {
            element.Attributes.Remove("style");
        }

        foreach (var childNode in node.ChildNodes.ToArray())
        {
            CleanNode(childNode, cancellationToken);
        }
    }
}
