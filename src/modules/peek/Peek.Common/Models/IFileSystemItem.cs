// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Windows.Storage;

#nullable enable

namespace Peek.Common.Models
{
    public interface IFileSystemItem
    {
        public DateTime DateModified => System.IO.File.GetCreationTime(Path);

        public string Extension => System.IO.Path.GetExtension(Path).ToLower(CultureInfo.InvariantCulture);

        public string Name => System.IO.Path.GetFileName(Path);

        public string Path { get; init; }

        public uint? Width
        {
            get
            {
                var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(Path);
                uint? width = propertyStore.TryGetUInt(PropertyKey.ImageHorizontalSize);
                Marshal.ReleaseComObject(propertyStore);
                return width;
            }
        }

        public uint? Height
        {
            get
            {
                var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(Path);
                uint? height = propertyStore.TryGetUInt(PropertyKey.ImageVerticalSize);
                Marshal.ReleaseComObject(propertyStore);
                return height;
            }
        }

        public ulong FileSizeBytes
        {
            get
            {
                var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(Path);
                ulong fileSize = propertyStore.TryGetULong(PropertyKey.FileSizeBytes) ?? 0;
                Marshal.ReleaseComObject(propertyStore);
                return fileSize;
            }
        }

        public string FileType
        {
            get
            {
                var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(Path);
                string fileType = propertyStore.TryGetString(PropertyKey.FileType) ?? string.Empty;
                Marshal.ReleaseComObject(propertyStore);
                return fileType;
            }
        }

        public Task<IStorageItem?> GetStorageItemAsync();
    }
}
