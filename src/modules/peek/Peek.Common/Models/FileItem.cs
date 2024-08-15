// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using ManagedCommon;
using Windows.Storage;

#nullable enable

namespace Peek.Common.Models;

public class FileItem(string path, string name) : IFileSystemItem
{
    public string Name { get; init; } = name;

    public string ParsingName => string.Empty;

    public string Path { get; init; } = path;

    public string Extension => System.IO.Path.GetExtension(Path).ToLower(CultureInfo.InvariantCulture);

    public async Task<IStorageItem?> GetStorageItemAsync() => await GetStorageFileAsync();

    public async Task<StorageFile?> GetStorageFileAsync()
    {
        try
        {
            // Don't cache these objects; they are cheap to create but seem to have a large (unmanaged) memory footprint.
            return await StorageFile.GetFileFromPathAsync(Path);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error getting file from path. " + ex.Message);
            return null;
        }
    }
}
