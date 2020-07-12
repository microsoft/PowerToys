using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using System.Linq;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Microsoft.Plugin.Program.Programs;
using Microsoft.Plugin.Program.Storage;
using System.IO;

namespace Microsoft.Plugin.Program.UnitTests.Storage
{
    using Win32 = Program.Programs.Win32;

    [TestFixture]
    class Win32ProgramRepositoryTest
    {
        List<IFileSystemWatcherWrapper> _fileSystemWatchers;
        Settings _settings = new Settings();
        string[] _pathsToWatch = new string[] { "location1", "location2" };
        List<Mock<IFileSystemWatcherWrapper>> _fileSystemMocks;

        [SetUp]
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

        [TestCase("Name", "ExecutableName", "FullPath", "description1", "description2")]
        public void Win32Repository_MustNotStoreDuplicates_WhileAddingItemsWithSameHashCode(string name, string exename, string fullPath, string description1, string description2)
        {
            // Arrange
            Win32ProgramRepository _win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32>>("Win32"), _settings, _pathsToWatch);

            Win32 item1 = new Win32 
            { 
                Name = name, 
                ExecutableName = exename,
                FullPath = fullPath,
                Description = description1
            };

            Win32 item2 = new Win32
            {
                Name = name,
                ExecutableName = exename,
                FullPath = fullPath,
                Description = description2
            };

            // Act
            _win32ProgramRepository.Add(item1);

            Assert.AreEqual(_win32ProgramRepository.Count(), 1);

            // To add an item with the same hashCode, ie, same name, exename and fullPath
            _win32ProgramRepository.Add(item2);

            // Assert, count still remains 1 because they are duplicate items
            Assert.AreEqual(_win32ProgramRepository.Count(), 1);
        }

        [TestCase("path.appref-ms")]
        [TestCase("path.exe")]
        public void Win32ProgramRepository_MustCallOnAppCreated_WhenCreatedEventIsRaised(string path)
        {
            // Arrange
            Win32ProgramRepository _win32ProgramRepository = new Win32ProgramRepository(_fileSystemWatchers, new BinaryStorage<IList<Win32>>("Win32"), _settings, _pathsToWatch);

            FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Created,"directory", path);
            _fileSystemMocks[0].Raise(m => m.Created += null, e);

            // Assert
            Assert.AreEqual(_win32ProgramRepository.Count(), 1);
            Assert.AreEqual(_win32ProgramRepository.ElementAt(0).AppType, 2);
        }
    }
}
