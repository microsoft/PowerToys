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

        public WoxJsonStorage()
            : this(typeof(T).Name)
        {
        }

        public WoxJsonStorage(string fileName)
        {
            var directoryPath = Path.Combine(Constant.DataDirectory, DirectoryName);
            Helper.ValidateDirectory(directoryPath);

            FilePath = Path.Combine(directoryPath, $"{fileName}{FileSuffix}");
        }
    }
}
