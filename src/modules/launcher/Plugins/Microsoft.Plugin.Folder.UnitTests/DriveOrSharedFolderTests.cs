// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Plugin.Folder.UnitTests
{
    [TestClass]
    public class DriveOrSharedFolderTests
    {
        [DataTestMethod]
        [DataRow(@"\\test-server\testdir", true)]
        [DataRow(@"c:", true)]
        [DataRow(@"c:\", true)]
        [DataRow(@"C:\", true)]
        [DataRow(@"d:\", true)]
        [DataRow(@"z:\", false)]
        [DataRow(@"nope.exe", false)]
        [DataRow(@"win32\test.dll", false)]
        [DataRow(@"a\b\c\d", false)]
        [DataRow(@"D", false)]
        [DataRow(@"ZZ:\test", false)]
        public void IsDriveOrSharedFolder_WhenCalled(string search, bool expectedSuccess)
        {
            // Setup
            var driveInformationMock = new Mock<IDriveInformation>();

            driveInformationMock.Setup(r => r.GetDriveNames())
                .Returns(() => new[] { "c:", "d:" });

            var folderLinksMock = new Mock<IFolderLinks>();
            var folderHelper = new FolderHelper(driveInformationMock.Object, folderLinksMock.Object);

            // Act
            var isDriveOrSharedFolder = folderHelper.IsDriveOrSharedFolder(search);

            // Assert
            Assert.AreEqual(expectedSuccess, isDriveOrSharedFolder);
        }

        [DataTestMethod]
        [DataRow('A', true)]
        [DataRow('C', true)]
        [DataRow('c', true)]
        [DataRow('Z', true)]
        [DataRow('z', true)]
        [DataRow('ª', false)]
        [DataRow('α', false)]
        [DataRow('Ω', false)]
        [DataRow('ɀ', false)]
        public void ValidDriveLetter_WhenCalled(char letter, bool expectedSuccess)
        {
            // Setup
            // Act
            var isDriveOrSharedFolder = FolderHelper.ValidDriveLetter(letter);

            // Assert
            Assert.AreEqual(expectedSuccess, isDriveOrSharedFolder);
        }

        [DataTestMethod]
        [DataRow("C:", true)]
        [DataRow("C:\test", true)]
        [DataRow("D:", false)]
        [DataRow("INVALID", false)]
        public void GenerateMaxFiles_WhenCalled(string search, bool hasValues)
        {
            // Setup
            var folderHelperMock = new Mock<IFolderHelper>();

            // Using Ordinal since this is used with paths
            folderHelperMock.Setup(r => r.IsDriveOrSharedFolder(It.IsAny<string>()))
                .Returns<string>(s => s.StartsWith("C:", StringComparison.Ordinal));

            var itemResultMock = new Mock<IItemResult>();

            var internalDirectoryMock = new Mock<IQueryInternalDirectory>();
            internalDirectoryMock.Setup(r => r.Query(It.IsAny<string>()))
                .Returns(new List<IItemResult>() { itemResultMock.Object });

            var processor = new InternalDirectoryProcessor(folderHelperMock.Object, internalDirectoryMock.Object);

            // Act
            var results = processor.Results(string.Empty, search);

            // Assert
            if (hasValues)
            {
                Assert.IsTrue(results.Count() > 0);
            }
            else
            {
                Assert.IsTrue(results.Count() == 0);
            }
        }
    }
}
