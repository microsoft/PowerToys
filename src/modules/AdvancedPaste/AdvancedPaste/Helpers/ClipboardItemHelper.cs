// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers
{
    internal static class ClipboardItemHelper
    {
        /// <summary>
        /// Creates a ClipboardItem from current clipboard data.
        /// </summary>
        public static async Task<ClipboardItem> CreateFromCurrentClipboardAsync(
            DataPackageView clipboardData,
            ClipboardFormat availableFormats,
            DateTimeOffset? timestamp = null,
            BitmapImage existingImage = null)
        {
            if (clipboardData == null || availableFormats == ClipboardFormat.None)
            {
                return null;
            }

            var clipboardItem = new ClipboardItem
            {
                Format = availableFormats,
                Timestamp = timestamp,
            };

            // Text or HTML content
            if (availableFormats.HasFlag(ClipboardFormat.Text) || availableFormats.HasFlag(ClipboardFormat.Html))
            {
                clipboardItem.Content = await clipboardData.GetTextOrEmptyAsync();
            }

            // Image content
            else if (availableFormats.HasFlag(ClipboardFormat.Image))
            {
                // Reuse existing image if provided
                if (existingImage != null)
                {
                    clipboardItem.Image = existingImage;
                }
                else
                {
                    clipboardItem.Image = await TryCreateBitmapImageAsync(clipboardData);
                }
            }

            return clipboardItem;
        }

        /// <summary>
        /// Creates a BitmapImage from clipboard data.
        /// </summary>
        private static async Task<BitmapImage> TryCreateBitmapImageAsync(DataPackageView clipboardData)
        {
            try
            {
                var imageReference = await clipboardData.GetBitmapAsync();
                if (imageReference != null)
                {
                    using (var imageStream = await imageReference.OpenReadAsync())
                    {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(imageStream);
                        return bitmapImage;
                    }
                }
            }
            catch
            {
                // Silently fail - caller can check for null
            }

            return null;
        }
    }
}
