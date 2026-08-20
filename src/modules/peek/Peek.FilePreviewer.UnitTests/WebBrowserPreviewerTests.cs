// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Models;
using Peek.FilePreviewer.Previewers;

namespace Peek.FilePreviewer.UnitTests
{
    [TestClass]
    public class WebBrowserPreviewerTests
    {
        [TestMethod]
        public void IsItemSupported_MarkdownExtension_ShouldReturnTrue()
        {
            var item = new FileItem(@"C:\some\file.md", "file.md");

            Assert.IsTrue(WebBrowserPreviewer.IsItemSupported(item));
        }

        [TestMethod]
        public void IsItemSupported_UnrecognizedExtension_ShouldReturnFalse()
        {
            var item = new FileItem(@"C:\some\file.zzzzunknown", "file.zzzzunknown");

            Assert.IsFalse(WebBrowserPreviewer.IsItemSupported(item));
        }

        [TestMethod]
        public void IsFallbackCandidate_UnrecognizedExtension_ShouldReturnTrue()
        {
            var item = new FileItem(@"C:\some\file.zzzzunknown", "file.zzzzunknown");

            Assert.IsTrue(WebBrowserPreviewer.IsFallbackCandidate(item));
        }

        // IsItemSupported should short-circuit the fallback for extensions Peek already recognizes.
        [TestMethod]
        public void IsFallbackCandidate_AlreadySupportedExtension_ShouldReturnFalse()
        {
            var item = new FileItem(@"C:\some\file.md", "file.md");

            Assert.IsFalse(WebBrowserPreviewer.IsFallbackCandidate(item));
        }

        [TestMethod]
        public void IsFallbackCandidate_FolderItem_ShouldReturnFalse()
        {
            var item = new FolderItem(@"C:\some\folder", "folder", "folder");

            Assert.IsFalse(WebBrowserPreviewer.IsFallbackCandidate(item));
        }

        [TestMethod]
        public void IsFallbackCandidate_MissingFile_ShouldReturnTrue()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zzzzunknown");
            var item = new FileItem(missingPath, Path.GetFileName(missingPath));

            Assert.IsTrue(WebBrowserPreviewer.IsFallbackCandidate(item));
        }
    }
}
