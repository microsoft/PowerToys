// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.IO.Abstractions;

namespace Hosts.Tests
{
    public class TestFileSystemWatcher : FileSystemWatcherBase
    {
        public override bool IncludeSubdirectories { get; set; }

        public override bool EnableRaisingEvents { get; set; }

        public override string Filter { get; set; }

        public override int InternalBufferSize { get; set; }

        public override NotifyFilters NotifyFilter { get; set; }

        public override string Path { get; set; }

        public override ISite Site { get; set; }

        public override ISynchronizeInvoke SynchronizingObject { get; set; }

        public override WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType) => default(WaitForChangedResult);

        public override WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout) => default(WaitForChangedResult);

        public TestFileSystemWatcher(string path) => Path = path;

        public TestFileSystemWatcher(string path, string filter)
        {
            Path = path;
            Filter = filter;
        }

        public override void BeginInit()
        {
        }

        public override void EndInit()
        {
        }
    }
}
