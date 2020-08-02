using System.Collections.ObjectModel;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    // File System Watcher Wrapper class which implements the IFileSystemWatcherWrapper interface
    public class FileSystemWatcherWrapper : FileSystemWatcher, IFileSystemWatcherWrapper
    {
        public FileSystemWatcherWrapper() { }

        Collection<string> IFileSystemWatcherWrapper.Filters
        {
            get => this.Filters;
            set
            {
                if (value != null)
                {
                    foreach (string filter in value)
                    {
                        this.Filters.Add(filter);
                    }
                }
            }
        }
    }
}
