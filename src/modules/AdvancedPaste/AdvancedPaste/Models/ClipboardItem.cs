// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Models;

public class ClipboardItem
{
    public string Content { get; set; }

    public BitmapImage Image { get; set; }

    public ClipboardFormat Format { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    // Only used for clipboard history items that have a ClipboardHistoryItem
    public ClipboardHistoryItem Item { get; set; }

    public string Description => !string.IsNullOrEmpty(Content) ? Content :
                                 Image is not null ? ResourceLoaderInstance.ResourceLoader.GetString("ClipboardHistoryImage") :
                                 string.Empty;
}
