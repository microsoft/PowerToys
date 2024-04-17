// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Hosts.Tests.Mocks;
using HostsUILib.Exceptions;
using HostsUILib.Helpers;
using HostsUILib.Models;
using HostsUILib.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Hosts.Tests
{
    [TestClass]
    public class HostsServiceTest
    {
        private static Mock<IUserSettings> _userSettings;
        private static Mock<IElevationHelper> _elevationHelper;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _userSettings = new Mock<IUserSettings>();
            _elevationHelper = new Mock<IElevationHelper>();
            _elevationHelper.Setup(m => m.IsElevated).Returns(true);
        }

        [TestMethod]
        public void Hosts_Exists()
        {
            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(string.Empty));
            var result = service.Exists();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Hosts_Not_Exists()
        {
            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
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

            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(content));

            var data = await service.ReadAsync();
            var entries = data.Entries.ToList();
            entries.Add(new Entry(0, "10.1.1.30", "host30 host30.local", "new entry", false));
            await service.WriteAsync(data.AdditionalLines, entries);

            var result = fileSystem.GetFile(service.HostsFilePath);
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

            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(content));

            var data = await service.ReadAsync();
            var entries = data.Entries.ToList();
            entries.RemoveAt(0);
            await service.WriteAsync(data.AdditionalLines, entries);

            var result = fileSystem.GetFile(service.HostsFilePath);
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

            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(content));

            var data = await service.ReadAsync();
            var entry = data.Entries[0];
            entry.Address = "10.1.1.10";
            entry.Hosts = "host host.local host1.local";
            entry.Comment = "updated comment";
            entry.Active = false;
            await service.WriteAsync(data.AdditionalLines, data.Entries);

            var result = fileSystem.GetFile(service.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task Empty_Hosts()
        {
            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(string.Empty));

            await service.WriteAsync(string.Empty, Enumerable.Empty<Entry>());

            var result = fileSystem.GetFile(service.HostsFilePath);
            Assert.AreEqual(result.TextContents, string.Empty);
        }

        [TestMethod]
        public async Task AdditionalLines_Top()
        {
            var content =
@"# header
10.1.1.1 host host.local   # comment
# comment
10.1.1.2 host2 host2.local # another comment
# footer
";

            var contentResult =
@"# header
# comment
# footer
10.1.1.1 host host.local   # comment
10.1.1.2 host2 host2.local # another comment
";

            var fileSystem = new CustomMockFileSystem();
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.AdditionalLinesPosition).Returns(HostsAdditionalLinesPosition.Top);
            var service = new HostsService(fileSystem, userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(content));

            var data = await service.ReadAsync();
            await service.WriteAsync(data.AdditionalLines, data.Entries);

            var result = fileSystem.GetFile(service.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task AdditionalLines_Bottom()
        {
            var content =
@"# header
10.1.1.1 host host.local   # comment
# comment
10.1.1.2 host2 host2.local # another comment
# footer
";

            var contentResult =
@"10.1.1.1 host host.local   # comment
10.1.1.2 host2 host2.local # another comment
# header
# comment
# footer
";

            var fileSystem = new CustomMockFileSystem();
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.AdditionalLinesPosition).Returns(HostsAdditionalLinesPosition.Bottom);
            var service = new HostsService(fileSystem, userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(content));

            var data = await service.ReadAsync();
            await service.WriteAsync(data.AdditionalLines, data.Entries);

            var result = fileSystem.GetFile(service.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task LongHosts_Splitted()
        {
            var content =
@"10.1.1.1 host01 host02 host03 host04 host05 host06 host07 host08 host09 host10 host11 host12 host13 host14 host15 host16 host17 host18 host19 # comment
";

            var contentResult =
@"10.1.1.1 host01 host02 host03 host04 host05 host06 host07 host08 host09 # comment
10.1.1.1 host10 host11 host12 host13 host14 host15 host16 host17 host18 # comment
10.1.1.1 host19                                                         # comment
";

            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            fileSystem.AddFile(service.HostsFilePath, new MockFileData(content));

            var data = await service.ReadAsync();
            await service.WriteAsync(data.AdditionalLines, data.Entries);

            var result = fileSystem.GetFile(service.HostsFilePath);
            Assert.AreEqual(result.TextContents, contentResult);
        }

        [TestMethod]
        public async Task Save_NotRunningElevatedException()
        {
            var fileSystem = new CustomMockFileSystem();
            var elevationHelper = new Mock<IElevationHelper>();
            elevationHelper.Setup(m => m.IsElevated).Returns(false);

            var service = new HostsService(fileSystem, _userSettings.Object, elevationHelper.Object);
            await Assert.ThrowsExceptionAsync<NotRunningElevatedException>(async () => await service.WriteAsync("# Empty hosts file", Enumerable.Empty<Entry>()));
        }

        [TestMethod]
        public async Task Save_ReadOnlyHostsException()
        {
            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            var hostsFile = new MockFileData(string.Empty);
            hostsFile.Attributes = System.IO.FileAttributes.ReadOnly;
            fileSystem.AddFile(service.HostsFilePath, hostsFile);

            await Assert.ThrowsExceptionAsync<ReadOnlyHostsException>(async () => await service.WriteAsync("# Empty hosts file", Enumerable.Empty<Entry>()));
        }

        [TestMethod]
        public void Remove_ReadOnly()
        {
            var fileSystem = new CustomMockFileSystem();
            var service = new HostsService(fileSystem, _userSettings.Object, _elevationHelper.Object);
            var hostsFile = new MockFileData(string.Empty);
            hostsFile.Attributes = System.IO.FileAttributes.ReadOnly;
            fileSystem.AddFile(service.HostsFilePath, hostsFile);

            service.RemoveReadOnly();
            var readOnly = fileSystem.FileInfo.FromFileName(service.HostsFilePath).Attributes.HasFlag(System.IO.FileAttributes.ReadOnly);
            Assert.IsFalse(readOnly);
        }
    }
}
