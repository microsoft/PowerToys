// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;

namespace Hosts.Tests.Mocks
{
    public class MockFileSystemWatcherFactory : IFileSystemWatcherFactory
    {
        public IFileSystemWatcher CreateNew() => new MockFileSystemWatcher(null);

        public IFileSystemWatcher CreateNew(string path) => new MockFileSystemWatcher(path);

        public IFileSystemWatcher CreateNew(string path, string filter) => new MockFileSystemWatcher(path, filter);

        public IFileSystemWatcher FromPath(string path) => new MockFileSystemWatcher(path);
    }
}
