// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common;
using Peek.Common.Models;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers
{
    public static class ThumbnailHelper
    {
        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        public static readonly NativeSize HighQualityThumbnailSize = new NativeSize { Width = 720, Height = 720, };
        public static readonly NativeSize LowQualityThumbnailSize = new NativeSize { Width = 256, Height = 256, };

        private static readonly NativeSize FallBackThumbnailSize = new NativeSize { Width = 96, Height = 96, };
        private static readonly NativeSize LastFallBackThumbnailSize = new NativeSize { Width = 32, Height = 32, };

        private static readonly List<NativeSize> ThumbnailFallBackSizes = new List<NativeSize>
        {
            HighQualityThumbnailSize,
            LowQualityThumbnailSize,
            FallBackThumbnailSize,
            LastFallBackThumbnailSize,
        };

        // TODO: Add a re-try system if there is no thumbnail of requested size.
        public static HResult GetThumbnail(string filename, out IntPtr hbitmap, NativeSize thumbnailSize)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = NativeMethods.SHCreateItemFromParsingName(filename, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.ScaleUp;

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(thumbnailSize, options, out hbitmap);

            // Try to get thumbnail using the fallback sizes order
            if (hr != HResult.Ok)
            {
                var currentThumbnailFallBackIndex = ThumbnailFallBackSizes.IndexOf(thumbnailSize);
                var nextThumbnailFallBackIndex = currentThumbnailFallBackIndex + 1;
                if (nextThumbnailFallBackIndex < ThumbnailFallBackSizes.Count - 1)
                {
                    hr = GetThumbnail(filename, out hbitmap, ThumbnailFallBackSizes[nextThumbnailFallBackIndex]);
                }
            }

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }

        public static async Task<BitmapImage?> GetThumbnailAsync(StorageFile? storageFile, uint size)
        {
            BitmapImage? bitmapImage = null;

            var imageStream = await storageFile?.GetThumbnailAsync(
                Windows.Storage.FileProperties.ThumbnailMode.SingleItem,
                size,
                Windows.Storage.FileProperties.ThumbnailOptions.None);

            if (imageStream == null)
            {
                return bitmapImage;
            }

            bitmapImage = new BitmapImage();
            bitmapImage.SetSource(imageStream);

            return bitmapImage;
        }
    }
}
