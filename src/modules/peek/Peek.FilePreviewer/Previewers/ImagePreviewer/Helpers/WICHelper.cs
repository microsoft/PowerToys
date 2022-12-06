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

                PropertyStoreShellApi.IPropertyStore? propertyStore = null;
                Guid iPropertyStoreGuid = typeof(PropertyStoreShellApi.IPropertyStore).GUID;
                PropertyStoreShellApi.SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, PropertyStoreShellApi.PropertyStoreFlags.READWRITE, ref iPropertyStoreGuid, out propertyStore);
                if (propertyStore != null)
                {
                    width = (int)PropertyStoreShellApi.GetUIntFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.ImageHorizontalSize);
                    height = (int)PropertyStoreShellApi.GetUIntFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.ImageVerticalSize);

                    Marshal.ReleaseComObject(propertyStore);
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
