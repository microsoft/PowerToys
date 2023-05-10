// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common;
using Peek.Common.Models;

namespace Peek.FilePreviewer.Previewers.Helpers
{
    public static class IconHelper
    {
        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        public static async Task<ImageSource?> GetIconAsync(string fileName, CancellationToken cancellationToken)
        {
            ImageSource? imageSource = null;
            IShellItem? nativeShellItem = null;
            try
            {
                Guid shellItem2Guid = new(IShellItem2Guid);
                int retCode = NativeMethods.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref shellItem2Guid, out nativeShellItem);

                if (retCode != 0)
                {
                    throw Marshal.GetExceptionForHR(retCode)!;
                }

                NativeSize large = new NativeSize { Width = 256, Height = 256 };
                var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.IconOnly;

                HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(large, options, out IntPtr hbitmap);

                cancellationToken.ThrowIfCancellationRequested();

                if (hr == HResult.Ok)
                {
                    imageSource = await BitmapHelper.GetBitmapFromHBitmapAsync(hbitmap, true, cancellationToken);
                }
                else
                {
                    var svgImageSource = new SvgImageSource(new Uri("ms-appx:///Assets/DefaultFileIcon.svg"));
                    imageSource = svgImageSource;
                }
            }
            finally
            {
                if (nativeShellItem != null)
                {
                    Marshal.ReleaseComObject(nativeShellItem);
                }
            }

            return imageSource;
        }
    }
}
