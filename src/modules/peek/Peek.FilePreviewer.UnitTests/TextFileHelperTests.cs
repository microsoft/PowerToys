// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.FilePreviewer.Previewers;

namespace Peek.FilePreviewer.UnitTests
{
    [TestClass]
    public class TextFileHelperTests
    {
        private string _tempFilePath = string.Empty;

        [TestInitialize]
        public void TestInitialize()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(_tempFilePath))
            {
                // File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public void IsTextFile_PlainAsciiContent_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Hello, world!\r\nThis is a plain text file.");

            Assert.IsTrue(TextFileHelper.IsTextFile(_tempFilePath));
        }

        [TestMethod]
        public void IsTextFile_EmptyFile_ShouldReturnTrue()
        {
            File.WriteAllBytes(_tempFilePath, Array.Empty<byte>());

            Assert.IsTrue(TextFileHelper.IsTextFile(_tempFilePath));
        }

        [TestMethod]
        public void IsTextFile_ContentWithNulByte_ShouldReturnFalse()
        {
            byte[] content = Encoding.UTF8.GetBytes("some text before");
            byte[] withNul = new byte[content.Length + 1];
            Array.Copy(content, withNul, content.Length);
            withNul[content.Length] = 0;
            File.WriteAllBytes(_tempFilePath, withNul);

            Assert.IsFalse(TextFileHelper.IsTextFile(_tempFilePath));
        }

        [TestMethod]
        public void IsTextFile_Utf8WithBom_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Text with a BOM", new UTF8Encoding(true));

            Assert.IsTrue(TextFileHelper.IsTextFile(_tempFilePath));
        }

        [TestMethod]
        public void IsTextFile_NonExistentFile_ShouldReturnFalse()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".missing");

            Assert.IsFalse(TextFileHelper.IsTextFile(missingPath));
        }

        // Only the first 8000 bytes are sniffed, so a NUL byte past that point must not affect the result.
        [TestMethod]
        public void IsTextFile_NulByteBeyondSampleWindow_ShouldReturnTrue()
        {
            byte[] buffer = new byte[9000];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'a';
            }

            buffer[8500] = 0;
            File.WriteAllBytes(_tempFilePath, buffer);

            Assert.IsTrue(TextFileHelper.IsTextFile(_tempFilePath));
        }

        [TestMethod]
        public void IsTextFile_NulByteWithinSampleWindow_ShouldReturnFalse()
        {
            byte[] buffer = new byte[9000];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'a';
            }

            buffer[7999] = 0;
            File.WriteAllBytes(_tempFilePath, buffer);

            Assert.IsFalse(TextFileHelper.IsTextFile(_tempFilePath));
        }
    }
}
