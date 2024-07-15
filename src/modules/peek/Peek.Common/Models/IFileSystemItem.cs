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
    public interface IFileSystemItem
    {
        public DateTime? DateModified
        {
            get
            {
                try
                {
                    return string.IsNullOrEmpty(Path) ? null : System.IO.File.GetCreationTime(Path);
                }
                catch
                {
                    return null;
                }
            }
        }

        public string Extension { get; }

        public string Name { get; }

        public string ParsingName { get; }

        public string Path { get; }

        public ulong FileSizeBytes => PropertyStoreHelper.TryGetUlongProperty(Path, PropertyKey.FileSizeBytes) ?? 0;

        public string FileType => PropertyStoreHelper.TryGetStringProperty(Path, PropertyKey.FileType) ?? string.Empty;

        public Task<IStorageItem?> GetStorageItemAsync();
    }
}
