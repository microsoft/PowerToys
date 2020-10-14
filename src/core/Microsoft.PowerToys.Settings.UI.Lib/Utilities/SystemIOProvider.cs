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

        public bool FileExists(string path)
        {
            return _fileSystem.File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return _fileSystem.File.ReadAllText(path);
        }
    }
}
