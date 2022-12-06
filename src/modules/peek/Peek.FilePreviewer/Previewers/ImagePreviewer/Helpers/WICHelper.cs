// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Peek.Common.Models;
    using WIC;

    public static class WICHelper
    {
        public static Task<Windows.Foundation.Size> GetImageSize(string filePath)
        {
            return Task.Run(() =>
            {
                int width = 0;
                int height = 0;

                ShellApi.IPropertyStore? propertyStore = null;
                Guid iPropertyStoreGuid = typeof(ShellApi.IPropertyStore).GUID;
                ShellApi.SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, ShellApi.PropertyStoreFlags.READWRITE, ref iPropertyStoreGuid, out propertyStore);
                if (propertyStore != null)
                {
                    var horizontalSizePropertyKey = new ShellApi.PropertyKey(new Guid(0x6444048F, 0x4C8B, 0x11D1, 0x8B, 0x70, 0x08, 0x00, 0x36, 0xB1, 0x1A, 0x03), 3);
                    var verticalSizePropertyKey = new ShellApi.PropertyKey(new Guid(0x6444048F, 0x4C8B, 0x11D1, 0x8B, 0x70, 0x08, 0x00, 0x36, 0xB1, 0x1A, 0x03), 4);
                    width = (int)ShellApi.GetUIntFromPropertyStore(propertyStore, horizontalSizePropertyKey);
                    height = (int)ShellApi.GetUIntFromPropertyStore(propertyStore, verticalSizePropertyKey);
                }

                if (width == 0 || height == 0)
                {
                    IWICImagingFactory factory = (IWICImagingFactory)new WICImagingFactoryClass();
                    var decoder = factory.CreateDecoderFromFilename(filePath, IntPtr.Zero, StreamAccessMode.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnLoad);
                    var frame = decoder?.GetFrame(0);

                    // TODO: Respect EXIF data and find correct orientation
                    frame?.GetSize(out width, out height);
                }

                return new Windows.Foundation.Size(width, height);
            });
        }
    }
}
