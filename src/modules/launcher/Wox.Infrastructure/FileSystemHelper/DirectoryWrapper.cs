// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Infrastructure.FileSystemHelper
{
    public class DirectoryWrapper : IDirectoryWrapper
    {
        public DirectoryWrapper()
        {
        }

        public bool Exists(string path)
        {
            return System.IO.Directory.Exists(path);
        }
    }
}
