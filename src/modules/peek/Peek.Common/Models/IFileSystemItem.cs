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
        public DateTime DateModified => System.IO.File.GetCreationTime(Path);

        public string Extension => System.IO.Path.GetExtension(Path).ToLower(CultureInfo.InvariantCulture);

        public string Name => System.IO.Path.GetFileName(Path);

        public string Path { get; init; }

        public IPropertyStore PropertyStore { get; }

        public Task<IStorageItem?> GetStorageItemAsync();
    }
}
