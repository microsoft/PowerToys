using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Wox.Infrastructure.Logger;
using System.Threading;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class FileWrapper : IFileWrapper
    {
        public FileWrapper() { }

        public string[] ReadAllLines(string path)
        {
            try
            {
                return File.ReadAllLines(path);
            }
            catch (IOException ex)
            {
                Log.Info($"File {path} is being accessed by another process| {ex.Message}");
                return new string[] { String.Empty };
            }
        }
    }
}
