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
            int attempt = 0;
            int maxRetries = 5;

            // Sometimes when files are being installed, url applications are written to line by line.
            // During this process their contents cannot be read as they are being accessed by an other process.
            // This ensures that if we do face this scenario, we retry after some time.
            while(attempt < maxRetries)
            {
                try
                {
                    return File.ReadAllLines(path);
                }
                catch (IOException ex)
                {
                    attempt++;
                    Thread.Sleep(500);
                    Log.Info($"File {path} is being accessed by another process| {ex.Message}");
                    
                }
            }

            return new string[] { String.Empty };
        }

    }
}
