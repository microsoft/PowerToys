using System.Diagnostics;
using System.IO;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class FileVersionInfoWrapper : IFileVersionInfoWrapper
    {
        public FileVersionInfoWrapper() { }
        public FileVersionInfo GetVersionInfo(string path)
        {
            if(File.Exists(path))
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
