// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal sealed class ImageMetadataProvider : IClipboardMetadataProvider
{
    public string SectionTitle => Resources.metadata_image_section_title;

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
                Key = Resources.metadata_image_dimensions_key,
                Data = new DetailsLink($"{metadata.Width} x {metadata.Height}"),
            });
            result.Add(new DetailsElement
            {
                Key = Resources.metadata_image_dpi_key,
                Data = new DetailsLink($"{metadata.DpiX:0.###} x {metadata.DpiY:0.###}"),
            });

            if (metadata.StorageSize != null)
            {
                result.Add(new DetailsElement
                {
                    Key = Resources.metadata_image_storage_size_key,
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
