// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
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
    }
}
