// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Models;
using Peek.FilePreviewer.Previewers;

namespace Peek.FilePreviewer.UnitTests
{
    [TestClass]
    public class WebBrowserPreviewerTests
    {
        private string _tempFilePath = string.Empty;

        [TestCleanup]
        public void TestCleanup()
        {
            if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        private string CreateTempFile(string extension, byte[] content)
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + extension);
            File.WriteAllBytes(_tempFilePath, content);
            return _tempFilePath;
        }

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
        public void IsTextFallbackSupported_UnrecognizedExtensionWithTextContent_ShouldReturnTrue()
        {
            string path = CreateTempFile(".zzzzunknown", Encoding.UTF8.GetBytes("plain text content"));
            var item = new FileItem(path, Path.GetFileName(path));

            Assert.IsTrue(WebBrowserPreviewer.IsTextFallbackSupported(item));
        }

        [TestMethod]
        public void IsTextFallbackSupported_UnrecognizedExtensionWithBinaryContent_ShouldReturnFalse()
        {
            string path = CreateTempFile(".zzzzunknown", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            var item = new FileItem(path, Path.GetFileName(path));

            Assert.IsFalse(WebBrowserPreviewer.IsTextFallbackSupported(item));
        }

        // IsItemSupported should short-circuit the fallback for extensions Peek already recognizes.
        [TestMethod]
        public void IsTextFallbackSupported_AlreadySupportedExtension_ShouldReturnFalse()
        {
            string path = CreateTempFile(".md", Encoding.UTF8.GetBytes("# heading"));
            var item = new FileItem(path, Path.GetFileName(path));

            Assert.IsFalse(WebBrowserPreviewer.IsTextFallbackSupported(item));
        }

        [TestMethod]
        public void IsTextFallbackSupported_FolderItem_ShouldReturnFalse()
        {
            var item = new FolderItem(@"C:\some\folder", "folder", "folder");

            Assert.IsFalse(WebBrowserPreviewer.IsTextFallbackSupported(item));
        }

        [TestMethod]
        public void IsTextFallbackSupported_MissingFile_ShouldReturnFalse()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zzzzunknown");
            var item = new FileItem(missingPath, Path.GetFileName(missingPath));

            Assert.IsFalse(WebBrowserPreviewer.IsTextFallbackSupported(item));
        }
    }
}
