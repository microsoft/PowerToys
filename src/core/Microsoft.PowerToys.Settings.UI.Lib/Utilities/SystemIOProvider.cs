// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;

namespace Microsoft.PowerToys.Settings.UI.Lib.Utilities
{
    public class SystemIOProvider : IIOProvider
    {
        private readonly IFileSystem _fileSystem;

        public SystemIOProvider()
            : this(new FileSystem())
        {
        }

        public SystemIOProvider(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public bool CreateDirectory(string path)
        {
            var directioryInfo = _fileSystem.Directory.CreateDirectory(path);
            return directioryInfo != null;
        }

        public void DeleteDirectory(string path)
        {
            _fileSystem.Directory.Delete(path, recursive: true);
        }

        public bool DirectoryExists(string path)
        {
            return _fileSystem.Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return _fileSystem.File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return _fileSystem.File.ReadAllText(path);
        }

        public void WriteAllText(string path, string content)
        {
            _fileSystem.File.WriteAllText(path, content);
        }
    }
}
