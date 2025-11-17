// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Hosts.Tests.Mocks
{
    public class CustomMockFileSystem : MockFileSystem
    {
        public override IFileSystemWatcherFactory FileSystemWatcher { get; }

        public CustomMockFileSystem()
            : base()
        {
            FileSystemWatcher = new MockFileSystemWatcherFactory();
        }
    }
}
