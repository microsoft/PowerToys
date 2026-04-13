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
        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a standard UNC path (\\server\share) is recognized
        /// Why: Baseline positive case — the most common UNC format
        /// </summary>
        [TestMethod]
        public void IsUncPath_StandardUncPath_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server\share"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a UNC path with nested subfolders and file is recognized
        /// Why: Real-world UNC paths include subfolders — must not fail on deeper paths
        /// </summary>
        [TestMethod]
        public void IsUncPath_UncPathWithSubfolder_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server\share\folder\file.txt"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a UNC path with dotted server name (FQDN) is recognized
        /// Why: Enterprise environments use FQDN server names (e.g., server.corp.com)
        /// </summary>
        [TestMethod]
        public void IsUncPath_UncPathWithDottedServer_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server.domain.com\share"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a UNC path with IP address is recognized
        /// Why: Some network shares use IP addresses instead of hostnames
        /// </summary>
        [TestMethod]
        public void IsUncPath_UncPathWithIPAddress_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\192.168.1.1\share"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a local drive path (C:\...) is not classified as UNC
        /// Why: Drive-letter paths are local — confusing them with UNC would break file access
        /// </summary>
        [TestMethod]
        public void IsUncPath_LocalDrivePath_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"C:\Users\test\file.txt"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a bare drive root (D:\) is not classified as UNC
        /// Why: Drive roots are local paths — shortest possible local absolute path
        /// </summary>
        [TestMethod]
        public void IsUncPath_LocalRootPath_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"D:\"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a relative path is not classified as UNC
        /// Why: Relative paths have no server component — Uri.TryCreate fails for them
        /// </summary>
        [TestMethod]
        public void IsUncPath_RelativePath_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"folder\file.txt"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies an empty string returns false without throwing
        /// Why: Empty input is a common edge case — must not crash
        /// </summary>
        [TestMethod]
        public void IsUncPath_EmptyString_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(string.Empty));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies an HTTP URL is not classified as UNC
        /// Why: HTTP URLs are not file paths — Uri.IsUnc must return false for them
        /// </summary>
        [TestMethod]
        public void IsUncPath_HttpUrl_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath("http://example.com/path"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a file:/// URI (local file) is not classified as UNC
        /// Why: file:///C:/... is a local file URI, not a network UNC path
        /// </summary>
        [TestMethod]
        public void IsUncPath_FileUri_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath("file:///C:/Users/test/file.txt"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a standard UNC path with a file component (\\server\share\file.txt)
        /// Why: Tests UNC with a trailing file name — distinct from share-only paths
        /// </summary>
        [TestMethod]
        public void IsUncPath_StandardUncWithFile_ShouldReturnTrue()
        {
            Assert.IsTrue(PathHelper.IsUncPath(@"\\server\share\file.txt"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies a single-backslash prefix is not classified as UNC
        /// Why: UNC requires exactly two leading backslashes — one backslash is not UNC
        /// </summary>
        [TestMethod]
        public void IsUncPath_SingleBackslash_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(@"\server\share"));
        }

        /// <summary>
        /// Product code: PathHelper.IsUncPath(string)
        /// What: Verifies null input returns false without throwing
        /// Why: Null is a common edge case — Uri.TryCreate handles null gracefully
        /// </summary>
        [TestMethod]
        public void IsUncPath_NullInput_ShouldReturnFalse()
        {
            Assert.IsFalse(PathHelper.IsUncPath(null));
        }
    }
}
