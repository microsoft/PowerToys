// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal static class DetailsMarkdownHelper
{
    private const int ImagePreviewMaxHeight = 200;

    public static string BuildTextBody(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var fence = "```";
        while (text.Contains(fence, StringComparison.Ordinal))
        {
            fence += "`";
        }

        return $"{fence}text\n{text}\n{fence}";
    }

    public static string BuildImageBody(string? imagePath, string altText)
        => string.IsNullOrEmpty(imagePath)
            ? string.Empty
            : $"![{altText}]({new Uri(imagePath).AbsoluteUri}?--x-cmdpal-fit=fit&--x-cmdpal-maxheight={ImagePreviewMaxHeight})";
}
