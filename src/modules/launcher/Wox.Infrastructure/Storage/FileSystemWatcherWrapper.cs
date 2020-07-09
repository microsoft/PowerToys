using System.Collections.ObjectModel;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    // File System Watcher Wrapper class which implements the IFileSystemWatcherWrapper interface
    public class FileSystemWatcherWrapper : IFileSystemWatcherWrapper
    {
        #region private_declarations
        private readonly FileSystemWatcher watcher;
        #endregion

        #region public_declarations

        // These are the three events that we are going to be listening to.
        // We would not be listening to the 'Changed' event handler as it is triggered whenever there is a change in the size, accessed timestamp etc.
        public event FileSystemEventHandler Created;
        public event FileSystemEventHandler Deleted;
        public event RenamedEventHandler Renamed;

        public FileSystemWatcherWrapper(FileSystemWatcher watcher)
        {
            this.watcher = watcher;
            watcher.Created += this.Created;
            watcher.Deleted += this.Deleted;
            watcher.Renamed += this.Renamed;
        }

        // Contains the regex filters for monitoring file types
        public Collection<string> Filters
        {
            get { return this.watcher.Filters; }
            set
            {
                // check on the value to be set
                if(value != null)
                {
                    foreach (string filter in value)
                    {
                        if (!string.IsNullOrEmpty(filter))
                        {
                            // Add the individual filters 
                            this.watcher.Filters.Add(filter);
                        }
                    }
                }
            }
        }

        // Contains information about whether the component is enabled/disabled
        public bool EnableRaisingEvents
        {
            get { return this.watcher.EnableRaisingEvents; }
            set { watcher.EnableRaisingEvents = value; }
        }

        // Contains the type of changes to monitor
        public NotifyFilters NotifyFilter
        {
            get { return this.watcher.NotifyFilter; }
            set { this.watcher.NotifyFilter = value; }
        }

        // Contains the path to monitor
        public string Path
        {
            get { return this.watcher.Path; }
            set
            {
                if(!string.IsNullOrEmpty(value))
                {
                    this.watcher.Path = value;
                }
            }
        }
        #endregion

    }
}
