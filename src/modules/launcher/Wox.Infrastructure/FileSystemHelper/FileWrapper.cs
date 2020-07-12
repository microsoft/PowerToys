using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class FileWrapper : IFileWrapper
    {
        public FileWrapper() { }

        public string[] ReadAllLines(string path)
        {
            return File.ReadAllLines(path);
        }

    }
}
