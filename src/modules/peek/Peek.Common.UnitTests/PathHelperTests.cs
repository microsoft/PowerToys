// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Helpers;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class PathHelperTests
    {
        [TestMethod]
        public void IsUncPath_StandardUncPath_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server\share"));
        }

        [TestMethod]
        public void IsUncPath_UncPathWithSubfolder_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server\share\folder\file.txt"));
        }

        [TestMethod]
        public void IsUncPath_UncPathWithDottedServer_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server.domain.com\share"));
        }

        [TestMethod]
        public void IsUncPath_UncPathWithIPAddress_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\192.168.1.1\share"));
        }

        [TestMethod]
        public void IsUncPath_LocalDrivePath_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"C:\Users\test\file.txt"));
        }

        [TestMethod]
        public void IsUncPath_LocalRootPath_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"D:\"));
        }

        [TestMethod]
        public void IsUncPath_RelativePath_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"folder\file.txt"));
        }

        [TestMethod]
        public void IsUncPath_EmptyString_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(string.Empty));
        }

        [TestMethod]
        public void IsUncPath_HttpUrl_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath("http://example.com/path"));
        }

        [TestMethod]
        public void IsUncPath_FileUri_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath("file:///C:/Users/test/file.txt"));
        }

        [TestMethod]
        public void IsUncPath_UncFileUri_ShouldReturnTrue()
        {
            // file://server/share is a UNC URI
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server\share\file.txt"));
        }

        [TestMethod]
        public void IsUncPath_SingleBackslash_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"\server\share"));
        }
    }
}
