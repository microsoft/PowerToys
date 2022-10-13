// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;

namespace Hosts.Tests
{
    public class TestFileSystemWatcherFactory : IFileSystemWatcherFactory
    {
        public IFileSystemWatcher CreateNew() => new TestFileSystemWatcher(null);

        public IFileSystemWatcher CreateNew(string path) => new TestFileSystemWatcher(path);

        public IFileSystemWatcher CreateNew(string path, string filter) => new TestFileSystemWatcher(path, filter);

        public IFileSystemWatcher FromPath(string path) => new TestFileSystemWatcher(path);
    }
}
