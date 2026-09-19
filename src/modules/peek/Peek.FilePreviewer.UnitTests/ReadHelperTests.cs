// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.FilePreviewer.Previewers;

namespace Peek.FilePreviewer.UnitTests
{
    [TestClass]
    public class ReadHelperTests
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
        public async Task Read_PlainTextFile_ShouldReturnContent()
        {
            File.WriteAllText(_tempFilePath, "Hello, world!", Encoding.UTF8);

            string content = await ReadHelper.Read(_tempFilePath);

            Assert.AreEqual("Hello, world!", content);
        }

        [TestMethod]
        public async Task Read_FileExceedsMaxSize_ShouldThrow()
        {
            byte[] buffer = new byte[ReadHelper.MaxReadableFileSizeBytes + 1];
            File.WriteAllBytes(_tempFilePath, buffer);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => ReadHelper.Read(_tempFilePath));
        }

        [TestMethod]
        public async Task Read_FileExceedsExplicitMaxSize_ShouldThrow()
        {
            File.WriteAllText(_tempFilePath, new string('a', 4096), Encoding.UTF8);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => ReadHelper.Read(_tempFilePath, maxReadableFileSizeBytes: 1024));
        }

        [TestMethod]
        public async Task Read_FileWithinExplicitMaxSize_ShouldReturnContent()
        {
            File.WriteAllText(_tempFilePath, "small enough", Encoding.UTF8);

            string content = await ReadHelper.Read(_tempFilePath, maxReadableFileSizeBytes: 1024);

            Assert.AreEqual("small enough", content);
        }

        [TestMethod]
        public async Task Read_Utf16LeFileWithBom_ShouldDecodeAndStripBom()
        {
            File.WriteAllText(_tempFilePath, "Unicode café", Encoding.Unicode);

            string content = await ReadHelper.Read(_tempFilePath);

            Assert.AreEqual("Unicode café", content);
        }

        [TestMethod]
        public async Task Read_Utf8FileWithBom_ShouldDecodeAndStripBom()
        {
            File.WriteAllText(_tempFilePath, "text with bom", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            string content = await ReadHelper.Read(_tempFilePath);

            Assert.AreEqual("text with bom", content);
        }

        [TestMethod]
        public async Task Read_Cancelled_ShouldThrow()
        {
            File.WriteAllText(_tempFilePath, "Hello, world!", Encoding.UTF8);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => ReadHelper.Read(_tempFilePath, ReadHelper.MaxReadableFileSizeBytes, cts.Token));
        }

        // Larger than the 64 KB charset-detection sample: the body must still be decoded to EOF,
        // not just the sampled prefix.
        [TestMethod]
        public async Task Read_FileLargerThanCharsetSample_ShouldReturnFullContent()
        {
            string expected = new string('a', 200_000);
            File.WriteAllText(_tempFilePath, expected, Encoding.UTF8);

            string content = await ReadHelper.Read(_tempFilePath);

            Assert.AreEqual(expected, content);
        }

        // File larger than the sample, non-UTF-8 encoding identified from a BOM in the sampled prefix:
        // the streamed decode must honour that encoding and strip the BOM for the whole body.
        [TestMethod]
        public async Task Read_LargeUtf16FileWithBom_ShouldDecodeFullContent()
        {
            string expected = "café résumé " + new string('x', 200_000);
            File.WriteAllText(_tempFilePath, expected, Encoding.Unicode);

            string content = await ReadHelper.Read(_tempFilePath);

            Assert.AreEqual(expected, content);
        }

        // File larger than the sample, dense non-ASCII UTF-8 (no BOM) in the sampled prefix: detection
        // should pick UTF-8 and every multi-byte character must survive the streamed decode.
        [TestMethod]
        public async Task Read_LargeUtf8FileNoBom_ShouldDecodeCorrectly()
        {
            string prefix = string.Concat(Enumerable.Repeat("café résumé naïve piñata ", 400));
            string expected = prefix + new string('x', 200_000);
            File.WriteAllText(_tempFilePath, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string content = await ReadHelper.Read(_tempFilePath);

            Assert.AreEqual(expected, content);
        }
    }
}
