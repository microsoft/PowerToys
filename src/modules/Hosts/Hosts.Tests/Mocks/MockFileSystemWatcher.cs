// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Abstractions;

namespace Hosts.Tests.Mocks
{
    public partial class MockFileSystemWatcher : FileSystemWatcherBase
    {
        public override bool IncludeSubdirectories { get; set; }

        public override bool EnableRaisingEvents { get; set; }

        public override string Filter { get; set; }

        public override int InternalBufferSize { get; set; }

        public override NotifyFilters NotifyFilter { get; set; }

        public override string Path { get; set; }

        public override ISite Site { get; set; }

        public override ISynchronizeInvoke SynchronizingObject { get; set; }

        public override Collection<string> Filters => throw new NotImplementedException();

        public override IFileSystem FileSystem => throw new NotImplementedException();

        public override IContainer Container => throw new NotImplementedException();

        public override void BeginInit() => throw new NotImplementedException();

        public override void EndInit() => throw new NotImplementedException();

        public override IWaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, TimeSpan timeout) => throw new NotImplementedException();

        public override IWaitForChangedResult WaitForChanged(WatcherChangeTypes changeType) => throw new NotImplementedException();

        public override IWaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout) => throw new NotImplementedException();

        public MockFileSystemWatcher()
        {
        }

        public MockFileSystemWatcher(string path)
        {
            Path = path;
        }

        public MockFileSystemWatcher(string path, string filter)
        {
            Path = path;
            Filter = filter;
        }
    }
}
