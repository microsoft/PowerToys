using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class DirectoryWrapper : IDirectoryWrapper
    {
        public DirectoryWrapper() { }

        public DirectoryInfo GetParent(string path)
        {
            return Directory.GetParent(path);
        }
    }
}
