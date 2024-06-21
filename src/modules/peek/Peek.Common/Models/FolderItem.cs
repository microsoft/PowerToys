// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Windows.Storage;

#nullable enable

namespace Peek.Common.Models
{
    public class FolderItem(string path, string name, string parsingName) : IFileSystemItem
    {
        private StorageFolder? storageFolder;

        public string Name { get; init; } = name;

        public string ParsingName { get; init; } = parsingName;

        public string Path { get; init; } = path;

        public string Extension => string.Empty;

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
                    storageFolder = string.IsNullOrEmpty(Path) ? null : await StorageFolder.GetFolderFromPathAsync(Path);
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
