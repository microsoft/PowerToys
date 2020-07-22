using System.Collections.ObjectModel;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    public interface IFileSystemWatcherWrapper
    {
        // Events to watch out for
        event FileSystemEventHandler Created;
        event FileSystemEventHandler Deleted;
        event FileSystemEventHandler Changed;
        event RenamedEventHandler Renamed;

        // Properties of File System watcher
        Collection<string> Filters { get; set; }
        bool EnableRaisingEvents { get; set; }
        NotifyFilters NotifyFilter { get; set; }
        string Path { get; set; }
        bool IncludeSubdirectories { get; set; }
    }
}
