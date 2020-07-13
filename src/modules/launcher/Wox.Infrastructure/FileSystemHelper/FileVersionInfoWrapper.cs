using System.Diagnostics;
using System.IO;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class FileVersionInfoWrapper : IFileVersionInfoWrapper
    {
        public FileVersionInfoWrapper() { }
        public FileVersionInfo GetVersionInfo(string path)
        {
            return FileVersionInfo.GetVersionInfo(path);
        }

        public string FileDescription { get; set; }
    }
}
