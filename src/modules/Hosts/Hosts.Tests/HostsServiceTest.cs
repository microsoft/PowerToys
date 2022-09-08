// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Hosts.Helpers;
using Hosts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.Tests
{
    [TestClass]
    public class HostsServiceTest
    {
        [TestMethod]
        public void Hosts_Exists()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { HostsService.HostsFilePath, new MockFileData(string.Empty) },
            })
            {
                FileSystemWatcher = new TestFileSystemWatcherFactory(),
            };

            var service = new HostsService(fileSystem);
            var result = service.Exists();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Hosts_Not_Exists()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
            })
            {
                FileSystemWatcher = new TestFileSystemWatcherFactory(),
            };

            var service = new HostsService(fileSystem);
            var result = service.Exists();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task Host_Added()
        {
            var content =
@"10.1.1.1 host host.local # comment
10.1.1.2 host2 host2.local # another comment
";

            var contentResult =
@"  10.1.1.1  host host.local     # comment
  10.1.1.2  host2 host2.local   # another comment
# 10.1.1.30 host30 host30.local # new entry
";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { HostsService.HostsFilePath, new MockFileData(content) },
            })
            {
                FileSystemWatcher = new TestFileSystemWatcherFactory(),
            };

            var service = new HostsService(fileSystem);

            var entries = await service.ReadAsync();
            entries.Add(new Entry("10.1.1.30", "host30 host30.local", "new entry", false));
            await service.WriteAsync(entries);

            var result = fileSystem.GetFile(HostsService.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task Host_Deleted()
        {
            var content =
@"10.1.1.1 host host.local # comment
10.1.1.2 host2 host2.local # another comment
";

            var contentResult =
@"10.1.1.2 host2 host2.local # another comment
";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { HostsService.HostsFilePath, new MockFileData(content) },
            })
            {
                FileSystemWatcher = new TestFileSystemWatcherFactory(),
            };

            var service = new HostsService(fileSystem);

            var entries = await service.ReadAsync();
            entries.RemoveAt(0);
            await service.WriteAsync(entries);

            var result = fileSystem.GetFile(HostsService.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task Host_Updated()
        {
            var content =
@"10.1.1.1 host host.local # comment
10.1.1.2 host2 host2.local # another comment
";

            var contentResult =
@"# 10.1.1.10 host host.local host1.local # updated comment
  10.1.1.2  host2 host2.local           # another comment
";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { HostsService.HostsFilePath, new MockFileData(content) },
            })
            {
                FileSystemWatcher = new TestFileSystemWatcherFactory(),
            };

            var service = new HostsService(fileSystem);

            var entries = await service.ReadAsync();
            var entry = entries[0];
            entry.Address = "10.1.1.10";
            entry.Hosts = "host host.local host1.local";
            entry.Comment = "updated comment";
            entry.Active = false;
            await service.WriteAsync(entries);

            var result = fileSystem.GetFile(HostsService.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task Empty_Hosts()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { HostsService.HostsFilePath, new MockFileData(string.Empty) },
            })
            {
                FileSystemWatcher = new TestFileSystemWatcherFactory(),
            };

            var service = new HostsService(fileSystem);
            await service.WriteAsync(Enumerable.Empty<Entry>());

            var result = fileSystem.GetFile(HostsService.HostsFilePath);
            Assert.AreEqual(result.TextContents, string.Empty);
        }
    }
}
