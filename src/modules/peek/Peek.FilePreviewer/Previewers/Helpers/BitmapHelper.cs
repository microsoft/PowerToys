// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common;

namespace Peek.FilePreviewer.Previewers.Helpers
{
    public static class BitmapHelper
    {
        public static async Task<BitmapSource> GetBitmapFromHBitmapAsync(IntPtr hbitmap, bool isSupportingTransparency, CancellationToken cancellationToken)
        {
            try
            {
                var bitmap = Image.FromHbitmap(hbitmap);
                if (isSupportingTransparency)
                {
                    bitmap.MakeTransparent();
                }

                var bitmapImage = new BitmapImage();

                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, isSupportingTransparency ? ImageFormat.Png : ImageFormat.Bmp);
                    stream.Position = 0;

                    cancellationToken.ThrowIfCancellationRequested();
                    await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                NativeMethods.DeleteObject(hbitmap);
            }
        }

        public static async Task<BitmapSource> GetBitmapFromHIconAsync(IntPtr hicon, CancellationToken cancellationToken)
        {
            try
            {
                var icon = (Icon)Icon.FromHandle(hicon).Clone();
                var bitmap = icon.ToBitmap();

                var bitmapImage = new BitmapImage();

                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;

                    cancellationToken.ThrowIfCancellationRequested();
                    await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            finally
            {
                // Delete HIcon to avoid memory leaks
                _ = NativeMethods.DestroyIcon(hicon);
            }
        }
    }
}
