// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

/// <summary>
/// Detects web links in text and shows normalized URL and key parts.
/// </summary>
internal sealed class WebLinkMetadataProvider : IClipboardMetadataProvider
{
    public string SectionTitle => "Link";

    public bool CanHandle(ClipboardItem item)
    {
        if (!item.IsText || string.IsNullOrWhiteSpace(item.Content))
        {
            return false;
        }

        if (!UrlHelper.IsValidUrl(item.Content))
        {
            return false;
        }

        var normalized = UrlHelper.NormalizeUrl(item.Content);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Exclude file: scheme; it's handled by TextFileSystemMetadataProvider
        return !uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
    }

    public IEnumerable<DetailsElement> GetDetails(ClipboardItem item)
    {
        var result = new List<DetailsElement>();
        if (!item.IsText || string.IsNullOrWhiteSpace(item.Content))
        {
            return result;
        }

        try
        {
            var normalized = UrlHelper.NormalizeUrl(item.Content);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                return result;
            }

            // Skip file: at runtime as well (defensive)
            if (uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            result.Add(new DetailsElement { Key = "URL", Data = new DetailsLink(normalized) });
            result.Add(new DetailsElement { Key = "Host", Data = new DetailsLink(uri.Host) });

            if (!uri.IsDefaultPort)
            {
                result.Add(new DetailsElement { Key = "Port", Data = new DetailsLink(uri.Port.ToString(CultureInfo.CurrentCulture)) });
            }

            if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            {
                result.Add(new DetailsElement { Key = "Path", Data = new DetailsLink(uri.AbsolutePath) });
            }

            if (!string.IsNullOrEmpty(uri.Query))
            {
                var q = uri.Query;
                var count = q.Count(static c => c == '&') + (q.Length > 1 ? 1 : 0);
                result.Add(new DetailsElement { Key = "Query params", Data = new DetailsLink(count.ToString(CultureInfo.CurrentCulture)) });
            }

            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                result.Add(new DetailsElement { Key = "Fragment", Data = new DetailsLink(uri.Fragment) });
            }
        }
        catch
        {
            // ignore malformed inputs
        }

        return result;
    }

    public IEnumerable<ProviderAction> GetActions(ClipboardItem item)
    {
        if (!CanHandle(item))
        {
            yield break;
        }

        var normalized = UrlHelper.NormalizeUrl(item.Content!);

        var open = new CommandContextItem(new OpenUrlCommand(normalized))
        {
            RequestedShortcut = KeyChords.OpenUrl,
        };
        yield return new ProviderAction(WellKnownActionIds.Open, open);
    }
}
