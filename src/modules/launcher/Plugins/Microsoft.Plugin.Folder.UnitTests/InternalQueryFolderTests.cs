// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.Folder.UnitTests
{
    [TestClass]
    public class InternalQueryFolderTests
    {
        private static IQueryFileSystemInfo _queryFileSystemInfoMock;
        private static MockFileSystem _fileSystem;

        [TestInitialize]
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

        [TestMethod]
        public void Query_ThrowsException_WhenCalledNull()
        {
            // Setup
            var queryInternalDirectory = new QueryInternalDirectory(new FolderSettings(), _queryFileSystemInfoMock, _fileSystem.Directory);

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => queryInternalDirectory.Query(null).ToArray());
        }

        [DataTestMethod]
        [DataRow(@"c", 0, 0, false, DisplayName = "String empty is nothing")]
        [DataRow(@"c:", 2, 1, false, DisplayName = "Root without \\")]
        [DataRow(@"c:\", 2, 1, false, DisplayName = "Normal root")]
        [DataRow(@"c:\Test", 2, 2, false, DisplayName = "Select yourself")]
        [DataRow(@"c:\not-exist", 2, 1, false, DisplayName = "Folder not exist, return root")]
        [DataRow(@"c:\not-exist\not-exist2", 0, 0, false, DisplayName = "Folder not exist, return root")]
        [DataRow(@"c:\bla.t", 2, 1, false, DisplayName = "Partial match file")]
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
            Assert.IsTrue(isDriveOrSharedFolder[typeof(FileItemResult)].Count() <= maxFolderSetting, "Files are not limited");
            Assert.IsTrue(isDriveOrSharedFolder[typeof(FolderItemResult)].Count() <= maxFolderSetting, "Folders are not limited");

            // Checks if CreateOpenCurrentFolder is displayed
            Assert.AreEqual(Math.Min(folders + files, 1), isDriveOrSharedFolder[typeof(CreateOpenCurrentFolderResult)].Count(), "CreateOpenCurrentFolder displaying is incorrect");

            Assert.AreEqual(truncated, isDriveOrSharedFolder[typeof(TruncatedItemResult)].Count() == 1, "CreateOpenCurrentFolder displaying is incorrect");
        }

        [DataTestMethod]
        [DataRow(@"c:\>", 3, 3, true, DisplayName = "Max Folder test recursive")]
        [DataRow(@"c:\Test>", 3, 3, true, DisplayName = "2 Folders recursive")]
        [DataRow(@"c:\not-exist>", 3, 3, true, DisplayName = "Folder not exist, return root recursive")]
        [DataRow(@"c:\not-exist\not-exist2>", 0, 0, false, DisplayName = "Folder not exist, return root recursive")]
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
            Assert.IsTrue(isDriveOrSharedFolder[typeof(FileItemResult)].Count() <= maxFolderSetting, "Files are not limited");
            Assert.IsTrue(isDriveOrSharedFolder[typeof(FolderItemResult)].Count() <= maxFolderSetting, "Folders are not limited");

            // Checks if CreateOpenCurrentFolder is displayed
            Assert.AreEqual(Math.Min(folders + files, 1), isDriveOrSharedFolder[typeof(CreateOpenCurrentFolderResult)].Count(), "CreateOpenCurrentFolder displaying is incorrect");

            Assert.AreEqual(truncated, isDriveOrSharedFolder[typeof(TruncatedItemResult)].Count() == 1, "CreateOpenCurrentFolder displaying is incorrect");
        }
    }
}
