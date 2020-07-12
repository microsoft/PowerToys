using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    public class PathWrapper : IPathWrapper
    {
        public PathWrapper() { }

        public string GetFileNameWithoutExtension(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        public string GetFileName(string path)
        {
            return Path.GetFileName(path);
        }
    }
}
