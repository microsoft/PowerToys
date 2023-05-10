// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Windows.Storage;

#nullable enable

namespace Peek.Common.Models
{
    public class FileItem : IFileSystemItem
    {
        private StorageFile? storageFile;

        private Lazy<IPropertyStore> _propertyStore;

        public FileItem(string path)
        {
            Path = path;
            var propertyStore = PropertyStoreHelper.GetPropertyStoreFromPath(Path);
            Width = propertyStore.TryGetUInt(PropertyKey.ImageHorizontalSize) ?? 0;
            Height = propertyStore.TryGetUInt(PropertyKey.ImageVerticalSize) ?? 0;
            FileSizeBytes = propertyStore.TryGetULong(PropertyKey.FileSizeBytes) ?? 0;
            FileType = propertyStore.TryGetString(PropertyKey.FileType) ?? string.Empty;

            // Release property store so it no longer holds the file open
            Marshal.FinalReleaseComObject(propertyStore);
        }

        public string Path { get; init; }

        public uint? Width { get; init; }

        public uint? Height { get; init; }

        public ulong FileSizeBytes { get; init; }

        public string FileType { get; init; }

        public async Task<IStorageItem?> GetStorageItemAsync()
        {
            return await GetStorageFileAsync();
        }

        public async Task<StorageFile?> GetStorageFileAsync()
        {
            if (storageFile == null)
            {
                try
                {
                    storageFile = await StorageFile.GetFileFromPathAsync(Path);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error getting file from path. " + ex.Message);
                    storageFile = null;
                }
            }

            return storageFile;
        }
    }
}
