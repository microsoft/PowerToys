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
using ImageResizer.Models.ResizeResults;
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

        private static readonly Action<int, double> NoOpProgress = (_, __) => { };

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
        public void Process_WhenAllExecutionsFail_AggregatesAllErrorResults()
        {
            var batch = CreateBatch(
                ["Image1.jpg", "Image2.jpg"],
                file => new ErrorResult(file, new IOException($"Error: {file}")));

            var results = batch.Process(NoOpProgress, CancellationToken.None).ToList();

            var errors = results.OfType<ErrorResult>();
            Assert.AreEqual(2, results.Count);

            var errorFiles = new List<string>();

            foreach (var error in errors)
            {
                errorFiles.Add(error.FilePath);
                Assert.AreEqual("Error: " + error.FilePath, error.Exception.Message);
            }

            foreach (var file in batch.Files)
            {
                CollectionAssert.Contains(errorFiles, file);
            }
        }

        [TestMethod]
        public void Process_WhenAllExecutionsSucceed_AggregatesAllSuccessResults()
        {
            var batch = CreateBatch(
                ["Image1.jpg", "Image2.jpg"],
                file => new SuccessResult(file));

            var results = batch.Process(NoOpProgress, CancellationToken.None).ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(2, results.OfType<SuccessResult>().Count());
        }

        [TestMethod]
        public void Process_WhenExecutionsHaveMixedResults_AggregatesAllResultsCorrectly()
        {
            const string ErrorMessage = "Cannot read file.";
            const string RecycleFailedMessage = "File locked.";

            var mock = new Mock<ResizeBatch>();

            var filesToProcess = new List<string>
            {
                "Success.jpg",
                "Error.jpg",
                "Warning.jpg",
            };

            mock.SetupGet(b => b.Files).Returns(filesToProcess);

            // Calling Execute returns each of our different results in turn.
            mock.Protected()
                .SetupSequence<ResizeResult>("Execute", ItExpr.IsAny<string>())
                .Returns(new SuccessResult(filesToProcess[0]))
                .Returns(new ErrorResult(filesToProcess[1], new IOException(ErrorMessage)))
                .Returns(new FileRecycleFailedResult(
                    filesToProcess[2],
                    "backup" + filesToProcess[2],
                    new IOException(RecycleFailedMessage)));

            var batch = mock.Object;

            var results = batch.Process(NoOpProgress, CancellationToken.None).ToList();

            Assert.AreEqual(3, results.Count);

            Assert.AreEqual(1, results.OfType<SuccessResult>().Count());
            Assert.AreEqual(1, results.OfType<ErrorResult>().Count());
            Assert.AreEqual(1, results.OfType<FileRecycleFailedResult>().Count());

            var successResult = results.OfType<SuccessResult>().Single();
            Assert.AreEqual(filesToProcess[0], successResult.FilePath);

            var errorResult = results.OfType<ErrorResult>().Single();
            Assert.AreEqual(filesToProcess[1], errorResult.FilePath);
            Assert.AreEqual(ErrorMessage, errorResult.Exception.Message);

            var warningResult = results.OfType<FileRecycleFailedResult>().Single();
            Assert.AreEqual(filesToProcess[2], warningResult.FilePath);
            Assert.AreEqual(RecycleFailedMessage, warningResult.Exception.Message);
        }

        [TestMethod]
        public void ProcessReportsProgress()
        {
            var batch = CreateBatch(
                ["Image1.jpg", "Image2.jpg"],
                _ => null);

            var calls = new ConcurrentBag<(int I, double Count)>();

            batch.Process(
                (i, count) => calls.Add((i, count)),
                CancellationToken.None);

            Assert.AreEqual(2, calls.Count);
        }

        private static ResizeBatch CreateBatch(
            ICollection<string> files, Func<string, ResizeResult> executeFunc)
        {
            var mock = new Mock<ResizeBatch>();

            mock.SetupGet(x => x.Files).Returns(files);

            mock.Protected()
                .Setup<ResizeResult>("Execute", ItExpr.IsAny<string>())
                .Returns(executeFunc);

            return mock.Object;
        }
    }
}
