// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers
{
    internal static partial class ClipboardItemHelper
    {
        // Compiled regex for better performance when checking multiple clipboard items
        private static readonly Regex HexColorRegex = HexColorCompiledRegex();

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
        /// Checks if text is a valid RGB hex color (e.g., #FFBFAB or #fff).
        /// </summary>
        public static bool IsRgbHexColor(string text)
        {
            if (text == null)
            {
                return false;
            }

            string trimmedText = text.Trim();
            if (trimmedText.Length > 7)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(trimmedText))
            {
                return false;
            }

            // Match #RGB or #RRGGBB format (case-insensitive)
            return HexColorRegex.IsMatch(trimmedText);
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

        [GeneratedRegex(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled)]
        private static partial Regex HexColorCompiledRegex();
    }
}
