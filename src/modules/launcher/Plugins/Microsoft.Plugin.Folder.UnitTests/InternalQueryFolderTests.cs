// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;
using Moq;
using NUnit.Framework;

namespace Microsoft.Plugin.Folder.UnitTests
{
    [TestFixture]
    public class InternalQueryFolderTests
    {
        private static readonly HashSet<string> DirectoryExist = new HashSet<string>()
        {
            @"c:",
            @"c:\",
            @"c:\Test\",
            @"c:\Test\A\",
            @"c:\Test\b\",
        };

        private static readonly HashSet<string> FilesExist = new HashSet<string>()
        {
            @"c:\bla.txt",
            @"c:\Test\test.txt",
            @"c:\Test\more-test.png",
        };

        private static Mock<IQueryFileSystemInfo> _queryFileSystemInfoMock;

        [SetUp]
        public void SetupMock()
        {
            var queryFileSystemInfoMock = new Mock<IQueryFileSystemInfo>();
            queryFileSystemInfoMock.Setup(r => r.Exists(It.IsAny<string>()))
                .Returns<string>(path => ContainsDirectory(path));

            queryFileSystemInfoMock.Setup(r => r.MatchFileSystemInfo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns<string, string, bool>(MatchFileSystemInfo);

            _queryFileSystemInfoMock = queryFileSystemInfoMock;
        }

        // Windows supports C:\\\\\ => C:\
        private static bool ContainsDirectory(string path)
        {
            return DirectoryExist.Contains(TrimDirectoryEnd(path));
        }

        private static string TrimDirectoryEnd(string path)
        {
            var trimEnd = path.TrimEnd('\\');

            if (path.EndsWith('\\'))
            {
                trimEnd += '\\';
            }

            return trimEnd;
        }

        private static IEnumerable<DisplayFileInfo> MatchFileSystemInfo(string search, string incompleteName, bool isRecursive)
        {
            Func<string, bool> folderSearchFunc;
            Func<string, bool> fileSearchFunc;
            switch (isRecursive)
            {
                case false:
                    // Using Ordinal since this is internal
                    folderSearchFunc = s => s.Equals(search, StringComparison.Ordinal);

                    var regexSearch = TrimDirectoryEnd(search);

                    fileSearchFunc = s => Regex.IsMatch(s, $"^{Regex.Escape(regexSearch)}[^\\\\]*$");
                    break;
                case true:
                    // Using Ordinal since this is internal
                    folderSearchFunc = s => s.StartsWith(search, StringComparison.Ordinal);
                    fileSearchFunc = s => s.StartsWith(search, StringComparison.Ordinal);
                    break;
            }

            var directories = DirectoryExist.Where(s => folderSearchFunc(s))
                .Select(dir => new DisplayFileInfo()
                {
                    Type = DisplayType.Directory,
                    FullName = dir,
                });

            var files = FilesExist.Where(s => fileSearchFunc(s))
                .Select(file => new DisplayFileInfo()
                {
                    Type = DisplayType.File,
                    FullName = file,
                });

            return directories.Concat(files);
        }

        [Test]
        public void Query_ThrowsException_WhenCalledNull()
        {
            // Setup
            var queryInternalDirectory = new QueryInternalDirectory(new FolderSettings(), _queryFileSystemInfoMock.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => queryInternalDirectory.Query(null).ToArray());
        }

        [TestCase(@"c", 0, 0, false, Reason = "String empty is nothing")]
        [TestCase(@"c:", 1, 1, false, Reason = "Root without \\")]
        [TestCase(@"c:\", 1, 1, false, Reason = "Normal root")]
        [TestCase(@"c:\Test", 1, 2, false, Reason = "Select yourself")]
        [TestCase(@"c:\>", 2, 2, true, Reason = "Max Folder test recursive")]
        [TestCase(@"c:\Test>", 2, 2, true, Reason = "2 Folders recursive")]
        [TestCase(@"c:\not-exist", 1, 1, false, Reason = "Folder not exist, return root")]
        [TestCase(@"c:\not-exist>", 2, 2, true, Reason = "Folder not exist, return root recursive")]
        [TestCase(@"c:\not-exist\not-exist2", 0, 0, false, Reason = "Folder not exist, return root")]
        [TestCase(@"c:\not-exist\not-exist2>", 0, 0, false, Reason = "Folder not exist, return root recursive")]
        [TestCase(@"c:\bla.t", 1, 1, false, Reason = "Partial match file")]
        public void Query_WhenCalled(string search, int folders, int files, bool truncated)
        {
            const int maxFolderSetting = 2;

            // Setup
            var folderSettings = new FolderSettings()
            {
                MaxFileResults = maxFolderSetting,
                MaxFolderResults = maxFolderSetting,
            };

            var queryInternalDirectory = new QueryInternalDirectory(folderSettings, _queryFileSystemInfoMock.Object);

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
