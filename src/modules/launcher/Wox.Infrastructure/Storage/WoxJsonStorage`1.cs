// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using Wox.Plugin;

namespace Wox.Infrastructure.Storage
{
    public class WoxJsonStorage<T> : JsonStorage<T>
        where T : new()
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;

        public WoxJsonStorage(string fileName = "")
        {
            var directoryPath = Path.Combine(Constant.DataDirectory, DirectoryName);
            Helper.ValidateDirectory(directoryPath);

            var filename = fileName != null && fileName.Length != 0 ? fileName : typeof(T).Name;
            FilePath = Path.Combine(directoryPath, $"{filename}{FileSuffix}");
        }
    }
}
