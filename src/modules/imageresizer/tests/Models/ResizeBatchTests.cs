// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
            var standardInput =
                "Image1.jpg" + EOL +
                "Image2.jpg";
            var args = new[]
            {
                "/d", "OutputDir",
                "Image3.jpg",
            };

            var result = ResizeBatch.FromCommandLine(
                new StringReader(standardInput),
                args);

            CollectionAssert.AreEquivalent(new List<string> { "Image1.jpg", "Image2.jpg", "Image3.jpg" }, result.Files.ToArray());

            Assert.AreEqual("OutputDir", result.DestinationDirectory);
        }

        /*[Fact]
        public void Process_executes_in_parallel()
        {
            var batch = CreateBatch(_ => Thread.Sleep(50));
            batch.Files.AddRange(
                Enumerable.Range(0, Environment.ProcessorCount)
                    .Select(i => "Image" + i + ".jpg"));

            var stopwatch = Stopwatch.StartNew();
            batch.Process(CancellationToken.None, (_, __) => { });
            stopwatch.Stop();

            Assert.InRange(stopwatch.ElapsedMilliseconds, 50, 99);
        }*/

        [TestMethod]
        public void ProcessAggregatesErrors()
        {
            var batch = CreateBatch(file => throw new Exception("Error: " + file));
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");

            var errors = batch.Process((_, __) => { }, CancellationToken.None).ToList();

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
        public void ProcessReportsProgress()
        {
            var batch = CreateBatch(_ => { });
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");
            var calls = new ConcurrentBag<(int i, double count)>();

            batch.Process(
                (i, count) => calls.Add((i, count)),
                CancellationToken.None);

            Assert.AreEqual(2, calls.Count);
        }

        private static ResizeBatch CreateBatch(Action<string> executeAction)
        {
            var mock = new Mock<ResizeBatch> { CallBase = true };
            mock.Protected().Setup("Execute", ItExpr.IsAny<string>()).Callback(executeAction);

            return mock.Object;
        }
    }
}
