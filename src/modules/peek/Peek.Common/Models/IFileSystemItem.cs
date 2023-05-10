// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
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

        public uint? Width => PropertyStoreHelper.TryGetUintProperty(Path, PropertyKey.ImageHorizontalSize);

        public uint? Height => PropertyStoreHelper.TryGetUintProperty(Path, PropertyKey.ImageVerticalSize);

        public ulong FileSizeBytes => PropertyStoreHelper.TryGetUlongProperty(Path, PropertyKey.FileSizeBytes) ?? 0;

        public string FileType => PropertyStoreHelper.TryGetStringProperty(Path, PropertyKey.FileType) ?? string.Empty;

        public Task<IStorageItem?> GetStorageItemAsync();
    }
}
