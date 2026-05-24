#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeBatchTests
    {
        private static readonly string EOL = Environment.NewLine;

        [TestMethod]
        public void FromCommandLineWorks()
        {
            // Use actual test files that exist in the test directory
            var testDir = Path.GetDirectoryName(typeof(ResizeBatchTests).Assembly.Location);
            var file1 = Path.Combine(testDir, "Test.jpg");
            var file2 = Path.Combine(testDir, "Test.png");
            var file3 = Path.Combine(testDir, "Test.gif");

            var standardInput =
                file1 + EOL +
                file2;
            var args = new[]
            {
                "/d", "OutputDir",
                file3,
            };

            var result = ResizeBatch.FromCommandLine(
                new StringReader(standardInput),
                args);

            var files = result.Files.Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEquivalent(new List<string> { "Test.jpg", "Test.png", "Test.gif" }, files);

            Assert.AreEqual("OutputDir", result.DestinationDirectory);
        }

        [TestMethod]
        public async Task ProcessAggregatesErrors()
        {
            var batch = CreateBatch(file => throw new InvalidOperationException("Error: " + file));
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");

            var errors = (await batch.ProcessAsync((_, __) => { }, CancellationToken.None)).ToList();

            Assert.AreEqual(2, errors.Count);

            var errorFiles = new List<string>();

            foreach (var error in errors)
            {
                errorFiles.Add(error.File);
                Assert.AreEqual("Error: " + error.File, error.Error);
            }

            foreach (var file in batch.Files)
            {
                CollectionAssert.Contains(errorFiles, file);
            }
        }

        [TestMethod]
        public async Task ProcessReportsProgress()
        {
            var batch = CreateBatch(_ => { });
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");
            var calls = new ConcurrentBag<(int I, double Count)>();

            await batch.ProcessAsync(
                (i, count) => calls.Add((i, count)),
                CancellationToken.None);

            Assert.AreEqual(2, calls.Count);
        }

        private static ResizeBatch CreateBatch(Action<string> executeAction)
        {
            var mock = new Mock<ResizeBatch> { CallBase = true };
            mock.Protected()
                .Setup<Task>("ExecuteAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<Settings>())
                .Returns((string file, Settings settings) =>
                {
                    executeAction(file);
                    return Task.CompletedTask;
                });

            return mock.Object;
        }
    }
}
