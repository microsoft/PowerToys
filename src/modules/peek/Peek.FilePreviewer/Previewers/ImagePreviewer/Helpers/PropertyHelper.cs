// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Windows.Foundation;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Peek.FilePreviewer.Previewers
{
    public static class PropertyHelper
    {
        public static Task<Size> GetImageSize(string filePath)
        {
            return Task.Run(() =>
            {
                var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(filePath, GETPROPERTYSTOREFLAGS.GPS_OPENSLOWITEM);
                if (propertyStore != null)
                {
                    var width = (int)propertyStore.GetUInt(PropertyKey.ImageHorizontalSize);
                    var height = (int)propertyStore.GetUInt(PropertyKey.ImageVerticalSize);

                    Marshal.ReleaseComObject(propertyStore);
                    return new Size(width, height);
                }

                return Size.Empty;
            });
        }

        public static Task<ulong> GetFileSizeInBytes(string filePath)
        {
            ulong bytes = 0;
            var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(filePath, GETPROPERTYSTOREFLAGS.GPS_OPENSLOWITEM);
            if (propertyStore != null)
            {
                bytes = propertyStore.GetULong(PropertyKey.FileSizeBytes);

                Marshal.ReleaseComObject(propertyStore);
            }

            return Task.FromResult(bytes);
        }

        public static Task<string> GetFileType(string filePath)
        {
            var type = string.Empty;
            var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(filePath, GETPROPERTYSTOREFLAGS.GPS_OPENSLOWITEM);
            if (propertyStore != null)
            {
                // TODO: find a way to get user friendly description
                type = propertyStore.GetString(PropertyKey.FileType);

                Marshal.ReleaseComObject(propertyStore);
            }

            return Task.FromResult(type);
        }
    }
}
