// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;
using Moq;
using NUnit.Framework;

namespace Microsoft.Plugin.Folder.UnitTests
{
    public class DriveOrSharedFolderTests
    {
        [TestCase(@"\\test-server\testdir", true)]
        [TestCase(@"c:", true)]
        [TestCase(@"c:\", true)]
        [TestCase(@"C:\", true)]
        [TestCase(@"d:\", true)]
        [TestCase(@"z:\", false)]
        [TestCase(@"nope.exe", false)]
        [TestCase(@"win32\test.dll", false)]
        [TestCase(@"a\b\c\d", false)]
        [TestCase(@"D", false)]
        [TestCase(@"ZZ:\test", false)]
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

        [TestCase('A', true)]
        [TestCase('C', true)]
        [TestCase('c', true)]
        [TestCase('Z', true)]
        [TestCase('z', true)]
        [TestCase('ª', false)]
        [TestCase('α', false)]
        [TestCase('Ω', false)]
        [TestCase('ɀ', false)]
        public void ValidDriveLetter_WhenCalled(char letter, bool expectedSuccess)
        {
            // Setup
            // Act
            var isDriveOrSharedFolder = FolderHelper.ValidDriveLetter(letter);

            // Assert
            Assert.AreEqual(expectedSuccess, isDriveOrSharedFolder);
        }

        [TestCase("C:", true)]
        [TestCase("C:\test", true)]
        [TestCase("D:", false)]
        [TestCase("INVALID", false)]
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
                CollectionAssert.IsNotEmpty(results);
            }
            else
            {
                CollectionAssert.IsEmpty(results);
            }
        }
    }
}
