// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal sealed class ImageMetadataProvider : IClipboardMetadataProvider
{
    public string SectionTitle => "Image metadata";

    public bool CanHandle(ClipboardItem item) => item.IsImage;

    public IEnumerable<DetailsElement> GetDetails(ClipboardItem item)
    {
        var result = new List<DetailsElement>();
        if (!CanHandle(item) || item.ImageData is null)
        {
            return result;
        }

        try
        {
            var metadata = ImageMetadataAnalyzer.GetAsync(item.ImageData).GetAwaiter().GetResult();

            result.Add(new DetailsElement
            {
                Key = "Dimensions",
                Data = new DetailsLink($"{metadata.Width} x {metadata.Height}"),
            });
            result.Add(new DetailsElement
            {
                Key = "DPI",
                Data = new DetailsLink($"{metadata.DpiX:0.###} x {metadata.DpiY:0.###}"),
            });

            if (metadata.StorageSize != null)
            {
                result.Add(new DetailsElement
                {
                    Key = "Storage size",
                    Data = new DetailsLink(SizeFormatter.FormatSize(metadata.StorageSize.Value)),
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Failed to retrieve image metadata:" + ex);
        }

        return result;
    }

    public IEnumerable<ProviderAction> GetActions(ClipboardItem item) => [];
}
