// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.Common.Models
{
    using System;
    using System.Threading.Tasks;
    using Windows.Storage;

#nullable enable

    public class File
    {
        private StorageFile? storageFile;

        public File(string path)
        {
            Path = path;
        }

        public string Path { get; init; }

        public string Extension => System.IO.Path.GetExtension(Path);

        public string FileName => System.IO.Path.GetFileName(Path);

        public async Task<StorageFile> GetStorageFileAsync()
        {
            if (storageFile == null)
            {
                storageFile = await StorageFile.GetFileFromPathAsync(Path);
            }

            return storageFile;
        }
    }
}
