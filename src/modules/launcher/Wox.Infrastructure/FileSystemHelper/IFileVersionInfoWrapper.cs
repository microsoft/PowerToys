using System.Diagnostics;

namespace Wox.Infrastructure.FileSystemHelper
{
    public interface IFileVersionInfoWrapper
    {
        FileVersionInfo GetVersionInfo(string path);
        string FileDescription { get; set; }
    }
}
