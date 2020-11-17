// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    // File System Watcher Wrapper class which implements the IFileSystemWatcherWrapper interface
    public sealed class FileSystemWatcherWrapper : FileSystemWatcher, IFileSystemWatcherWrapper
    {
        public FileSystemWatcherWrapper()
        {
        }

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
