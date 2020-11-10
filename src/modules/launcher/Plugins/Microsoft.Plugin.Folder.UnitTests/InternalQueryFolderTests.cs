// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;
using NUnit.Framework;

namespace Microsoft.Plugin.Folder.UnitTests
{
    [TestFixture]
    public class InternalQueryFolderTests
    {
        private static IQueryFileSystemInfo _queryFileSystemInfoMock;
        private static MockFileSystem _fileSystem;

        [SetUp]
        public void SetupMock()
        {
            // Note: This mock filesystem adds a 'c:\temp' directory.
            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                { @"c:\bla.txt", new MockFileData(string.Empty) },
                { @"c:\Test\test.txt", new MockFileData(string.Empty) },
                { @"c:\Test\more-test.png", new MockFileData(string.Empty) },
                { @"c:\Test\A\deep-nested.png", new MockFileData(string.Empty) },
                { @"c:\Test\b\", new MockDirectoryData() },
            });

            _queryFileSystemInfoMock = new QueryFileSystemInfo(_fileSystem.DirectoryInfo);
        }

        [Test]
        public void Query_ThrowsException_WhenCalledNull()
        {
            // Setup
            var queryInternalDirectory = new QueryInternalDirectory(new FolderSettings(), _queryFileSystemInfoMock, _fileSystem.Directory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => queryInternalDirectory.Query(null).ToArray());
        }

        [TestCase(@"c", 0, 0, false, Reason = "String empty is nothing")]
        [TestCase(@"c:", 2, 1, false, Reason = "Root without \\")]
        [TestCase(@"c:\", 2, 1, false, Reason = "Normal root")]
        [TestCase(@"c:\Test", 2, 2, false, Reason = "Select yourself")]
        [TestCase(@"c:\not-exist", 2, 1, false, Reason = "Folder not exist, return root")]
        [TestCase(@"c:\not-exist\not-exist2", 0, 0, false, Reason = "Folder not exist, return root")]
        [TestCase(@"c:\bla.t", 2, 1, false, Reason = "Partial match file")]
        public void Query_WhenCalled(string search, int folders, int files, bool truncated)
        {
            const int maxFolderSetting = 3;

            // Setup
            var folderSettings = new FolderSettings()
            {
                MaxFileResults = maxFolderSetting,
                MaxFolderResults = maxFolderSetting,
            };

            var queryInternalDirectory = new QueryInternalDirectory(folderSettings, _queryFileSystemInfoMock, _fileSystem.Directory);

            // Act
            var isDriveOrSharedFolder = queryInternalDirectory.Query(search)
                .ToLookup(r => r.GetType());

            // Assert
            Assert.AreEqual(files, isDriveOrSharedFolder[typeof(FileItemResult)].Count(), "File count doesn't match");
            Assert.AreEqual(folders, isDriveOrSharedFolder[typeof(FolderItemResult)].Count(), "folder count doesn't match");

            // Always check if there is less than max folders
            Assert.LessOrEqual(isDriveOrSharedFolder[typeof(FileItemResult)].Count(), maxFolderSetting, "Files are not limited");
            Assert.LessOrEqual(isDriveOrSharedFolder[typeof(FolderItemResult)].Count(), maxFolderSetting, "Folders are not limited");

            // Checks if CreateOpenCurrentFolder is displayed
            Assert.AreEqual(Math.Min(folders + files, 1), isDriveOrSharedFolder[typeof(CreateOpenCurrentFolderResult)].Count(), "CreateOpenCurrentFolder displaying is incorrect");

            Assert.AreEqual(truncated, isDriveOrSharedFolder[typeof(TruncatedItemResult)].Count() == 1, "CreateOpenCurrentFolder displaying is incorrect");
        }

        [TestCase(@"c:\>", 3, 3, true, Reason = "Max Folder test recursive")]
        [TestCase(@"c:\Test>", 3, 3, true, Reason = "2 Folders recursive")]
        [TestCase(@"c:\not-exist>", 3, 3, true, Reason = "Folder not exist, return root recursive")]
        [TestCase(@"c:\not-exist\not-exist2>", 0, 0, false, Reason = "Folder not exist, return root recursive")]
        public void Query_Recursive_WhenCalled(string search, int folders, int files, bool truncated)
        {
            const int maxFolderSetting = 3;

            // Setup
            var folderSettings = new FolderSettings()
            {
                MaxFileResults = maxFolderSetting,
                MaxFolderResults = maxFolderSetting,
            };

            var queryInternalDirectory = new QueryInternalDirectory(folderSettings, _queryFileSystemInfoMock, _fileSystem.Directory);

            // Act
            var isDriveOrSharedFolder = queryInternalDirectory.Query(search)
                .ToLookup(r => r.GetType());

            // Assert
            Assert.AreEqual(files, isDriveOrSharedFolder[typeof(FileItemResult)].Count(), "File count doesn't match");
            Assert.AreEqual(folders, isDriveOrSharedFolder[typeof(FolderItemResult)].Count(), "folder count doesn't match");

            // Always check if there is less than max folders
            Assert.LessOrEqual(isDriveOrSharedFolder[typeof(FileItemResult)].Count(), maxFolderSetting, "Files are not limited");
            Assert.LessOrEqual(isDriveOrSharedFolder[typeof(FolderItemResult)].Count(), maxFolderSetting, "Folders are not limited");

            // Checks if CreateOpenCurrentFolder is displayed
            Assert.AreEqual(Math.Min(folders + files, 1), isDriveOrSharedFolder[typeof(CreateOpenCurrentFolderResult)].Count(), "CreateOpenCurrentFolder displaying is incorrect");

            Assert.AreEqual(truncated, isDriveOrSharedFolder[typeof(TruncatedItemResult)].Count() == 1, "CreateOpenCurrentFolder displaying is incorrect");
        }
    }
}
