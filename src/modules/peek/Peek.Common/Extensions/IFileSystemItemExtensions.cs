// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Peek.Common.Models;
using Scripting;
using Windows.Foundation;
using Windows.Storage;

namespace Peek.Common.Extensions
{
    public static class IFileSystemItemExtensions
    {
        public static Size? GetImageSize(this IFileSystemItem item)
        {
            Size? size = null;

            var propertyStore = item.PropertyStore;
            var width = propertyStore.TryGetUInt(PropertyKey.ImageHorizontalSize);
            var height = propertyStore.TryGetUInt(PropertyKey.ImageVerticalSize);

            if (width != null && height != null)
            {
                size = new Size((int)width, (int)height);
            }

            return size;
        }

        public static ulong GetSizeInBytes(this IFileSystemItem item)
        {
            ulong sizeInBytes = 0;

            switch (item)
            {
                case FolderItem _:
                    FileSystemObject fileSystemObject = new FileSystemObject();
                    Folder folder = fileSystemObject.GetFolder(item.Path);
                    sizeInBytes = (ulong)folder.Size;
                    break;
                case FileItem _:
                    var propertyStore = item.PropertyStore;
                    sizeInBytes = propertyStore.TryGetULong(PropertyKey.FileSizeBytes) ?? 0;
                    break;
            }

            return sizeInBytes;
        }

        public static async Task<string> GetContentTypeAsync(this IFileSystemItem item)
        {
            string contentType = string.Empty;

            var storageItem = await item.GetStorageItemAsync();
            switch (storageItem)
            {
                case StorageFile storageFile:
                    contentType = storageFile.DisplayType;
                    break;
                case StorageFolder storageFolder:
                    contentType = storageFolder.DisplayType;
                    break;
                default:
                    var propertyStore = item.PropertyStore;
                    contentType = propertyStore.TryGetString(PropertyKey.FileType) ?? string.Empty;
                    break;
            }

            return contentType;
        }
    }
}
