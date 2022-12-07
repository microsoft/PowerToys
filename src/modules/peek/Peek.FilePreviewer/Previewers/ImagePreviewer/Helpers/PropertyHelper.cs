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

        public static Task<int> GetFileSizeInBytes(string filePath)
        {
            return Task.Run(() =>
            {
                // TODO: find out why property store is null
                Guid iPropertyStoreGuid = typeof(PropertyStoreShellApi.IPropertyStore).GUID;
                PropertyStoreShellApi.IPropertyStore? propertyStore;
                PropertyStoreShellApi.SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, PropertyStoreShellApi.PropertyStoreFlags.READWRITE, ref iPropertyStoreGuid, out propertyStore);
                if (propertyStore != null)
                {
                    var bytes = (int)PropertyStoreShellApi.GetUIntFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.FileSizeBytes);

                    Marshal.ReleaseComObject(propertyStore);
                    return bytes;
                }

                return 0;
            });
        }

        public static Task<string> GetFileType(string filePath)
        {
            return Task.Run(() =>
            {
                // TODO: find out why property store is null
                Guid iPropertyStoreGuid = typeof(PropertyStoreShellApi.IPropertyStore).GUID;
                PropertyStoreShellApi.IPropertyStore? propertyStore;
                PropertyStoreShellApi.SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, PropertyStoreShellApi.PropertyStoreFlags.READWRITE, ref iPropertyStoreGuid, out propertyStore);
                if (propertyStore != null)
                {
                    var type = PropertyStoreShellApi.GetStringFromPropertyStore(propertyStore, PropertyStoreShellApi.PropertyKey.FileType);

                    Marshal.ReleaseComObject(propertyStore);
                    return type;
                }

                return string.Empty;
            });
        }
    }
}
