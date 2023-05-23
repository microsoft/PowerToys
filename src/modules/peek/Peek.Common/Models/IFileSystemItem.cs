// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
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
                DateTime? dateModified = null;
                try
                {
                    dateModified = System.IO.File.GetCreationTime(Path);
                }
                catch
                {
                    dateModified = null;
                }

                return dateModified;
            }
        }

        public string Extension => System.IO.Path.GetExtension(Path).ToLower(CultureInfo.InvariantCulture);

        public string Name { get; init; }

        public string Path { get; init; }

        public ulong FileSizeBytes => PropertyStoreHelper.TryGetUlongProperty(Path, PropertyKey.FileSizeBytes) ?? 0;

        public string FileType => PropertyStoreHelper.TryGetStringProperty(Path, PropertyKey.FileType) ?? string.Empty;

        public Task<IStorageItem?> GetStorageItemAsync();
    }
}
