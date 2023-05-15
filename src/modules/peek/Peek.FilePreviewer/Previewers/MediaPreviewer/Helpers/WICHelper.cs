// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Peek.Common.WIC;

namespace Peek.FilePreviewer.Previewers
{
    public static class WICHelper
    {
        public static Task<Windows.Foundation.Size> GetImageSize(string filePath)
        {
            return Task.Run(() =>
            {
                // TODO: Find a way to get file metadata without hydrating files. Look into Shell API/Windows Property System, e.g., IPropertyStore
                IWICImagingFactory factory = (IWICImagingFactory)new WICImagingFactory();
                var decoder = factory.CreateDecoderFromFilename(filePath, IntPtr.Zero, StreamAccessMode.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnLoad);
                var frame = decoder?.GetFrame(0);
                int width = 0;
                int height = 0;

                // TODO: Respect EXIF data and find correct orientation
                frame?.GetSize(out width, out height);

                return new Windows.Foundation.Size(width, height);
            });
        }
    }
}
