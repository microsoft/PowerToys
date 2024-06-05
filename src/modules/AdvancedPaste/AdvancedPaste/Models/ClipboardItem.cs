// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Models
{
    public class ClipboardItem
    {
        public string Content { get; set; }

        public ClipboardHistoryItem Item { get; set; }

        public BitmapImage Image { get; set; }
    }
}
