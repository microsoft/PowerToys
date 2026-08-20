// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public async Task IsTextFile_PlainAsciiContent_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Hello, world!\r\nThis is a plain text file.");

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_EmptyFile_ShouldReturnTrue()
        {
            File.WriteAllBytes(_tempFilePath, Array.Empty<byte>());

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_ContentWithNulByte_ShouldReturnFalse()
        {
            byte[] content = Encoding.UTF8.GetBytes("some text before");
            byte[] withNul = new byte[content.Length + 1];
            Array.Copy(content, withNul, content.Length);
            withNul[content.Length] = 0;
            File.WriteAllBytes(_tempFilePath, withNul);

            Assert.IsFalse(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_Utf8WithBom_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Text with a BOM", new UTF8Encoding(true));

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_Utf16LeWithBom_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Text with a UTF-16LE BOM", Encoding.Unicode);

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_Utf16BeWithBom_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Text with a UTF-16BE BOM", Encoding.BigEndianUnicode);

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_Utf32LeWithBom_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Text with a UTF-32LE BOM", new UTF32Encoding(bigEndian: false, byteOrderMark: true));

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_Utf32BeWithBom_ShouldReturnTrue()
        {
            File.WriteAllText(_tempFilePath, "Text with a UTF-32BE BOM", new UTF32Encoding(bigEndian: true, byteOrderMark: true));

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_FileExceedsMaxSize_ShouldReturnFalse()
        {
            byte[] buffer = new byte[ReadHelper.MaxReadableFileSizeBytes + 1];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'a';
            }

            File.WriteAllBytes(_tempFilePath, buffer);

            Assert.IsFalse(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_NonExistentFile_ShouldReturnFalse()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".missing");

            Assert.IsFalse(await TextFileHelper.IsTextFileAsync(missingPath, CancellationToken.None));
        }

        // Only the first 8000 bytes are sniffed, so a NUL byte past that point must not affect the result.
        [TestMethod]
        public async Task IsTextFile_NulByteBeyondSampleWindow_ShouldReturnTrue()
        {
            byte[] buffer = new byte[9000];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'a';
            }

            buffer[8500] = 0;
            File.WriteAllBytes(_tempFilePath, buffer);

            Assert.IsTrue(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_NulByteWithinSampleWindow_ShouldReturnFalse()
        {
            byte[] buffer = new byte[9000];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'a';
            }

            buffer[7999] = 0;
            File.WriteAllBytes(_tempFilePath, buffer);

            Assert.IsFalse(await TextFileHelper.IsTextFileAsync(_tempFilePath, CancellationToken.None));
        }

        [TestMethod]
        public async Task IsTextFile_Cancelled_ShouldThrow()
        {
            File.WriteAllText(_tempFilePath, "Hello, world!");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => TextFileHelper.IsTextFileAsync(_tempFilePath, cts.Token));
        }
    }
}
