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
    public class FolderItem : IFileSystemItem
    {
        private StorageFolder? storageFolder;

        private Lazy<IPropertyStore> _propertyStore;

        public FolderItem(string path)
        {
            Path = path;
            _propertyStore = new(() => PropertyStoreHelper.GetPropertyStoreFromPath(Path));
        }

        public string Path { get; init; }

        public IPropertyStore PropertyStore => _propertyStore.Value;

        public async Task<IStorageItem?> GetStorageItemAsync()
        {
            return await GetStorageFolderAsync();
        }

        public async Task<StorageFolder?> GetStorageFolderAsync()
        {
            if (storageFolder == null)
            {
                try
                {
                    storageFolder = await StorageFolder.GetFolderFromPathAsync(Path);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error getting folder from path. " + ex.Message);
                    storageFolder = null;
                }
            }

            return storageFolder;
        }
    }
}
