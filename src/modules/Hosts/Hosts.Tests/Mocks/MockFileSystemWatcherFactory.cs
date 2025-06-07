// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;

namespace Hosts.Tests.Mocks
{
    public class MockFileSystemWatcherFactory : IFileSystemWatcherFactory
    {
        public IFileSystem FileSystem => throw new NotImplementedException();

        public IFileSystemWatcher New() => new MockFileSystemWatcher();

        public IFileSystemWatcher New(string path) => new MockFileSystemWatcher(path);

        public IFileSystemWatcher New(string path, string filter) => new MockFileSystemWatcher(path, filter);

        public IFileSystemWatcher Wrap(FileSystemWatcher fileSystemWatcher) => throw new NotImplementedException();
    }
}
