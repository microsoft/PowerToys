// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Peek.Common.Models;
    using Windows.Foundation;

    public static class PropertyHelper
    {
        public static Task<Size> GetImageSize(string filePath)
        {
            return Task.Run(() =>
            {
                var propertyStore = PropertyStoreShellApi.GetPropertyStoreFromPath(filePath, PropertyStoreShellApi.PropertyStoreFlags.OPENSLOWITEM);
                if (propertyStore != null)
                {
                    var width = (int)PropertyStoreShellApi.GetUIntFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.ImageHorizontalSize);
                    var height = (int)PropertyStoreShellApi.GetUIntFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.ImageVerticalSize);

                    Marshal.ReleaseComObject(propertyStore);
                    return new Size(width, height);
                }

                return Size.Empty;
            });
        }

        public static Task<ulong> GetFileSizeInBytes(string filePath)
        {
            ulong bytes = 0;
            var propertyStore = PropertyStoreShellApi.GetPropertyStoreFromPath(filePath, PropertyStoreShellApi.PropertyStoreFlags.OPENSLOWITEM);
            if (propertyStore != null)
            {
                bytes = PropertyStoreShellApi.GetULongFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.FileSizeBytes);

                Marshal.ReleaseComObject(propertyStore);
            }

            return Task.FromResult(bytes);
        }

        public static Task<string> GetFileType(string filePath)
        {
            var type = string.Empty;
            var propertyStore = PropertyStoreShellApi.GetPropertyStoreFromPath(filePath, PropertyStoreShellApi.PropertyStoreFlags.OPENSLOWITEM);
            if (propertyStore != null)
            {
                // TODO: find a way to get user friendly description
                type = PropertyStoreShellApi.GetStringFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.FileType);

                Marshal.ReleaseComObject(propertyStore);
            }

            return Task.FromResult(type);
        }
    }
}
