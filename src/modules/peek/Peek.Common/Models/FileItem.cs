// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
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
            _propertyStore = new(() => PropertyStoreHelper.GetPropertyStoreFromPath(Path));
        }

        public string Path { get; init; }

        public IPropertyStore PropertyStore => _propertyStore.Value;

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
