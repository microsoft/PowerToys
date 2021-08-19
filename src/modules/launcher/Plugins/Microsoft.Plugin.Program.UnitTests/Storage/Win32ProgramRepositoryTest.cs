// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Plugin.Program.Programs;
using Microsoft.Plugin.Program.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Infrastructure.FileSystemHelper;
using Wox.Infrastructure.Storage;
using Win32Program = Microsoft.Plugin.Program.Programs.Win32Program;

namespace Microsoft.Plugin.Program.UnitTests.Storage
{
    [TestClass]
    public class Win32ProgramRepositoryTest
    {
        private readonly ProgramPluginSettings _settings = new ProgramPluginSettings();
        private readonly string[] _pathsToWatch = new string[] { "location1", "location2" };

        private List<IFileSystemWatcherWrapper> _fileSystemWatchers;
        private List<Mock<IFileSystemWatcherWrapper>> _fileSystemMocks;

        [TestInitialize]
        public void SetFileSystemWatchers()
        {
            _fileSystemWatchers = new List<IFileSystemWatcherWrapper>();
            _fileSystemMocks = new List<Mock<IFileSystemWatcherWrapper>>();
            for (int index = 0; index < _pathsToWatch.Length; index++)
            {
                var mockFileWatcher = new Mock<IFileSystemWatcherWrapper>();
                _fileSystemMocks.Add(mockFileWatcher);
                _fileSystemWatchers.Add(mockFileWatcher.Object);
            }
        }

        [DataTestMethod]
        [DataRow("Name", "ExecutableName", "FullPath", "description1", "description2")]
        public void Win32RepositoryMustNotStoreDuplicatesWhileAddingItemsWithSameHashCode(string name, string exename, string fullPath, string description1, string description2)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);

            Win32Program item1 = new Win32Program
            {
                Name = name,
                ExecutableName = exename,
                FullPath = fullPath,
                Description = description1,
            };

            Win32Program item2 = new Win32Program
            {
                Name = name,
                ExecutableName = exename,
                FullPath = fullPath,
                Description = description2,
            };

            // Act
            win32ProgramRepository.Add(item1);

            Assert.AreEqual(1, win32ProgramRepository.Count());

            // To add an item with the same hashCode, ie, same name, exename and fullPath
            win32ProgramRepository.Add(item2);

            // Assert, count still remains 1 because they are duplicate items
            Assert.AreEqual(1, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("path.appref-ms")]
        public void Win32ProgramRepositoryMustCallOnAppCreatedForApprefAppsWhenCreatedEventIsRaised(string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Created, "directory", path);

            // Act
            _fileSystemMocks[0].Raise(m => m.Created += null, e);

            // Assert
            Assert.AreEqual(1, win32ProgramRepository.Count());
            Assert.AreEqual(Win32Program.ApplicationType.ApprefApplication, win32ProgramRepository.ElementAt(0).AppType);
        }

        [DataTestMethod]
        [DataRow("directory", "path.appref-ms")]
        public void Win32ProgramRepositoryMustCallOnAppDeletedForApprefAppsWhenDeletedEventIsRaised(string directory, string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Deleted, directory, path);

            string fullPath = directory + "\\" + path;
            Win32Program item = Win32Program.GetAppFromPath(fullPath);
            win32ProgramRepository.Add(item);

            // Act
            _fileSystemMocks[0].Raise(m => m.Deleted += null, e);

            // Assert
            Assert.AreEqual(0, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("directory", "oldpath.appref-ms", "newpath.appref-ms")]
        public void Win32ProgramRepositoryMustCallOnAppRenamedForApprefAppsWhenRenamedEventIsRaised(string directory, string oldpath, string newpath)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            RenamedEventArgs e = new RenamedEventArgs(WatcherChangeTypes.Renamed, directory, newpath, oldpath);

            string oldFullPath = directory + "\\" + oldpath;
            string newFullPath = directory + "\\" + newpath;

            Win32Program olditem = Win32Program.GetAppFromPath(oldFullPath);
            Win32Program newitem = Win32Program.GetAppFromPath(newFullPath);
            win32ProgramRepository.Add(olditem);

            // Act
            _fileSystemMocks[0].Raise(m => m.Renamed += null, e);

            // Assert
            Assert.AreEqual(1, win32ProgramRepository.Count());
            Assert.IsTrue(win32ProgramRepository.Contains(newitem));
            Assert.IsFalse(win32ProgramRepository.Contains(olditem));
        }

        [DataTestMethod]
        [DataRow("path.exe")]
        public void Win32ProgramRepositoryMustCallOnAppCreatedForExeAppsWhenCreatedEventIsRaised(string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Created, "directory", path);

            // FileVersionInfo must be mocked for exe applications
            var mockFileVersionInfo = new Mock<IFileVersionInfoWrapper>();
            mockFileVersionInfo.Setup(m => m.GetVersionInfo(It.IsAny<string>())).Returns((FileVersionInfo)null);
            Win32Program.FileVersionInfoWrapper = mockFileVersionInfo.Object;

            // Act
            _fileSystemMocks[0].Raise(m => m.Created += null, e);

            // Assert
            Assert.AreEqual(1, win32ProgramRepository.Count());
            Assert.AreEqual(Win32Program.ApplicationType.Win32Application, win32ProgramRepository.ElementAt(0).AppType);
        }

        [DataTestMethod]
        [DataRow("directory", "path.exe")]
        public void Win32ProgramRepositoryMustCallOnAppDeletedForExeAppsWhenDeletedEventIsRaised(string directory, string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Deleted, directory, path);

            // FileVersionInfo must be mocked for exe applications
            var mockFileVersionInfo = new Mock<IFileVersionInfoWrapper>();
            mockFileVersionInfo.Setup(m => m.GetVersionInfo(It.IsAny<string>())).Returns((FileVersionInfo)null);
            Win32Program.FileVersionInfoWrapper = mockFileVersionInfo.Object;

            string fullPath = directory + "\\" + path;
            Win32Program item = Win32Program.GetAppFromPath(fullPath);
            win32ProgramRepository.Add(item);

            // Act
            _fileSystemMocks[0].Raise(m => m.Deleted += null, e);

            // Assert
            Assert.AreEqual(0, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("directory", "oldpath.appref-ms", "newpath.appref-ms")]
        public void Win32ProgramRepositoryMustCallOnAppRenamedForExeAppsWhenRenamedEventIsRaised(string directory, string oldpath, string newpath)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            RenamedEventArgs e = new RenamedEventArgs(WatcherChangeTypes.Renamed, directory, newpath, oldpath);

            string oldFullPath = directory + "\\" + oldpath;
            string newFullPath = directory + "\\" + newpath;

            // FileVersionInfo must be mocked for exe applications
            var mockFileVersionInfo = new Mock<IFileVersionInfoWrapper>();
            mockFileVersionInfo.Setup(m => m.GetVersionInfo(It.IsAny<string>())).Returns((FileVersionInfo)null);
            Win32Program.FileVersionInfoWrapper = mockFileVersionInfo.Object;

            Win32Program olditem = Win32Program.GetAppFromPath(oldFullPath);
            Win32Program newitem = Win32Program.GetAppFromPath(newFullPath);
            win32ProgramRepository.Add(olditem);

            // Act
            _fileSystemMocks[0].Raise(m => m.Renamed += null, e);

            // Assert
            Assert.AreEqual(1, win32ProgramRepository.Count());
            Assert.IsTrue(win32ProgramRepository.Contains(newitem));
            Assert.IsFalse(win32ProgramRepository.Contains(olditem));
        }

        [DataTestMethod]
        [DataRow("path.url")]
        public void Win32ProgramRepositoryMustNotCreateUrlAppWhenCreatedEventIsRaised(string path)
        {
            // We are handing internet shortcut apps using the Changed event instead

            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Created, "directory", path);

            // File.ReadAllLines must be mocked for url applications
            var mockFile = new Mock<IFile>();
            mockFile.Setup(m => m.ReadAllLines(It.IsAny<string>())).Returns(new string[] { "URL=steam://rungameid/1258080", "IconFile=iconFile" });
            Win32Program.FileWrapper = mockFile.Object;

            // Act
            _fileSystemMocks[0].Raise(m => m.Created += null, e);

            // Assert
            Assert.AreEqual(0, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("path.exe")]
        [DataRow("path.lnk")]
        [DataRow("path.appref-ms")]
        public void Win32ProgramRepositoryMustNotCreateAnyAppOtherThanUrlAppWhenChangedEventIsRaised(string path)
        {
            // We are handing internet shortcut apps using the Changed event instead

            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Changed, "directory", path);

            // FileVersionInfo must be mocked for exe applications
            var mockFileVersionInfo = new Mock<IFileVersionInfoWrapper>();
            mockFileVersionInfo.Setup(m => m.GetVersionInfo(It.IsAny<string>())).Returns((FileVersionInfo)null);
            Win32Program.FileVersionInfoWrapper = mockFileVersionInfo.Object;

            // ShellLinkHelper must be mocked for lnk applications
            var mockShellLink = new Mock<IShellLinkHelper>();
            mockShellLink.Setup(m => m.RetrieveTargetPath(It.IsAny<string>())).Returns(string.Empty);
            Win32Program.ShellLinkHelper = mockShellLink.Object;

            // Act
            _fileSystemMocks[0].Raise(m => m.Changed += null, e);

            // Assert
            Assert.AreEqual(0, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("directory", "path.url")]
        public void Win32ProgramRepositoryMustCallOnAppDeletedForUrlAppsWhenDeletedEventIsRaised(string directory, string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Deleted, directory, path);

            // File.ReadAllLines must be mocked for url applications
            var mockFile = new Mock<IFile>();
            mockFile.Setup(m => m.ReadLines(It.IsAny<string>())).Returns(new string[] { "URL=steam://rungameid/1258080", "IconFile=iconFile" });
            Win32Program.FileWrapper = mockFile.Object;

            string fullPath = directory + "\\" + path;
            Win32Program item = Win32Program.GetAppFromPath(fullPath);
            win32ProgramRepository.Add(item);

            // Act
            _fileSystemMocks[0].Raise(m => m.Deleted += null, e);

            // Assert
            Assert.AreEqual(0, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("directory", "oldpath.url", "newpath.url")]
        public void Win32ProgramRepositoryMustCallOnAppRenamedForUrlAppsWhenRenamedEventIsRaised(string directory, string oldpath, string newpath)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            RenamedEventArgs e = new RenamedEventArgs(WatcherChangeTypes.Renamed, directory, newpath, oldpath);

            // File.ReadAllLines must be mocked for url applications
            var mockFile = new Mock<IFile>();
            mockFile.Setup(m => m.ReadLines(It.IsAny<string>())).Returns(new string[] { "URL=steam://rungameid/1258080", "IconFile=iconFile" });
            Win32Program.FileWrapper = mockFile.Object;

            string oldFullPath = directory + "\\" + oldpath;
            string newFullPath = directory + "\\" + newpath;

            Win32Program olditem = Win32Program.GetAppFromPath(oldFullPath);
            Win32Program newitem = Win32Program.GetAppFromPath(newFullPath);
            win32ProgramRepository.Add(olditem);

            // Act
            _fileSystemMocks[0].Raise(m => m.Renamed += null, e);

            // Assert
            Assert.AreEqual(1, win32ProgramRepository.Count());
            Assert.IsTrue(win32ProgramRepository.Contains(newitem));
            Assert.IsFalse(win32ProgramRepository.Contains(olditem));
        }

        [DataTestMethod]
        [DataRow("directory", "path.lnk")]
        public void Win32ProgramRepositoryMustCallOnAppDeletedForLnkAppsWhenDeletedEventIsRaised(string directory, string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Deleted, directory, path);

            // ShellLinkHelper must be mocked for lnk applications
            var mockShellLink = new Mock<IShellLinkHelper>();
            mockShellLink.Setup(m => m.RetrieveTargetPath(It.IsAny<string>())).Returns(string.Empty);
            Win32Program.ShellLinkHelper = mockShellLink.Object;

            string fullPath = directory + "\\" + path;
            Win32Program item = new Win32Program
            {
                Name = "path",
                ExecutableName = "path.exe",
                ParentDirectory = "directory",
                FullPath = "directory\\path.exe",
                LnkResolvedPath = "directory\\path.lnk", // This must be equal for lnk applications
            };
            win32ProgramRepository.Add(item);

            // Act
            _fileSystemMocks[0].Raise(m => m.Deleted += null, e);

            // Assert
            Assert.AreEqual(0, win32ProgramRepository.Count());
        }

        [DataTestMethod]
        [DataRow("directory", "oldpath.lnk", "path.lnk")]
        public void Win32ProgramRepositoryMustCallOnAppRenamedForLnkAppsWhenRenamedEventIsRaised(string directory, string oldpath, string path)
        {
            // Arrange
            Win32ProgramRepository win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32Program>>("Win32"), _settings, _pathsToWatch);
            RenamedEventArgs e = new RenamedEventArgs(WatcherChangeTypes.Renamed, directory, path, oldpath);

            string oldFullPath = directory + "\\" + oldpath;
            string fullPath = directory + "\\" + path;

            // ShellLinkHelper must be mocked for lnk applications
            var mockShellLink = new Mock<IShellLinkHelper>();
            mockShellLink.Setup(m => m.RetrieveTargetPath(It.IsAny<string>())).Returns(string.Empty);
            Win32Program.ShellLinkHelper = mockShellLink.Object;

            // old item and new item are the actual items when they are in existence
            Win32Program olditem = new Win32Program
            {
                Name = "oldpath",
                ExecutableName = oldpath,
                FullPath = fullPath,
            };

            Win32Program newitem = new Win32Program
            {
                Name = "path",
                ExecutableName = path,
                FullPath = fullPath,
            };

            win32ProgramRepository.Add(olditem);

            // Act
            _fileSystemMocks[0].Raise(m => m.Renamed += null, e);

            // Assert
            Assert.AreEqual(1, win32ProgramRepository.Count());
            Assert.IsTrue(win32ProgramRepository.Contains(newitem));
            Assert.IsFalse(win32ProgramRepository.Contains(olditem));
        }
    }
}
