// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// DataTemplateSelector for HeroImage content that chooses between image and icon templates
/// based on whether the IconInfo contains image data or icon data.
/// </summary>
public partial class HeroImageDataTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Template used for image content (when IconData has Data property set)
    /// </summary>
    public DataTemplate? ImageTemplate { get; set; }

    /// <summary>
    /// Template used for icon content (when IconData has Icon property set)
    /// </summary>
    public DataTemplate? IconTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is IconInfo iconInfo)
        {
            // Check if this is image content by looking at the Light IconData
            // If Data is not null, it's an image; otherwise it's an icon
            if (iconInfo.Light?.Data != null)
            {
                return ImageTemplate;
            }
            else
            {
                return IconTemplate;
            }
        }

        // Fallback to icon template for unknown types
        return IconTemplate;
    }
}
