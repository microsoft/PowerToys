// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class FileVersionInfoWrapper : IFileVersionInfoWrapper
    {
        public FileVersionInfoWrapper()
        {
        }

        public FileVersionInfo GetVersionInfo(string path)
        {
            if (File.Exists(path))
            {
                return FileVersionInfo.GetVersionInfo(path);
            }
            else
            {
                return null;
            }
        }

        public string FileDescription { get; set; }
    }
}
