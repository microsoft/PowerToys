#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using Moq;
using Moq.Protected;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeBatchTests
    {
        [TestMethod]
        [DataRow(1)]
        [DataRow(3)]
        public void FromCommandLineWorks(int fileCount)
        {
            using var directory = new TestDirectory();
            var files = new List<string>
            {
                CopyTestImage(directory, "選択 1.JpG"),
            };
            if (fileCount == 3)
            {
                files.Add(CopyTestImage(directory, "café 2.PnG", "Test.png"));
                files.Add(CopyTestImage(directory, "画像 3.GIF", "Test.gif"));
            }

            var destination = Path.Combine(directory, "出力 folder");
            Directory.CreateDirectory(destination);
            var args = new List<string> { "/d", destination };
            var expected = files.ToArray();
            if (fileCount == 3)
            {
                args.Add(files[2]);
                expected = new[] { files[2], files[0], files[1] };
            }

            var unsupported = Path.Combine(directory, "notes.txt");
            var missing = Path.Combine(directory, "missing.jpg");
            File.WriteAllText(unsupported, "not an image");
            var streamFiles = fileCount == 3 ? files.Take(2) : files;
            using var standardInput = new StringReader(
                string.Join("\r\n", streamFiles.Concat(new[] { unsupported, missing })) + "\r\n");

            var result = ResizeBatch.FromCommandLine(
                standardInput,
                args.ToArray());

            CollectionAssert.AreEqual(expected, result.Files.ToArray());
            Assert.AreEqual(destination, result.DestinationDirectory);
            Assert.AreEqual(0, result.InputErrors.Count);
        }

        [TestMethod]
        public async Task ProcessAggregatesErrors()
        {
            var batch = CreateBatch(file => throw new InvalidOperationException("Error: " + file));
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");

            var errors = (await batch.ProcessAsync((_, __) => { }, new Settings(), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10))).ToList();

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
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public async Task ProcessReportsProgress(int fileCount)
        {
            var settings = new Settings();
            var attempts = new ConcurrentBag<(string File, Settings Settings)>();
            var batch = CreateBatch((file, actualSettings) =>
            {
                attempts.Add((file, actualSettings));
                return Task.CompletedTask;
            });
            for (var i = 0; i < fileCount; i++)
            {
                batch.Files.Add($"Image{i}.jpg");
            }

            var calls = new ConcurrentBag<(int I, double Count)>();

            var errors = await batch.ProcessAsync(
                (i, count) => calls.Add((i, count)),
                settings,
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, errors.Count());
            CollectionAssert.AreEquivalent(batch.Files.ToArray(), attempts.Select(attempt => attempt.File).ToArray());
            foreach (var attempt in attempts)
            {
                Assert.AreSame(settings, attempt.Settings);
            }

            AssertProgress(calls, fileCount);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(3)]
        public async Task ProcessContinuesAfterErrorAndReportsBasename(int fileCount)
        {
            var failedFile = @"root\failed.jpg";
            var attempts = new ConcurrentBag<string>();
            var batch = CreateBatch(file =>
            {
                attempts.Add(file);
                if (file == failedFile)
                {
                    throw new InvalidOperationException("bad");
                }
            });
            batch.Files.Add(failedFile);
            for (var i = 1; i < fileCount; i++)
            {
                batch.Files.Add($@"root\success{i}.jpg");
            }

            var calls = new ConcurrentBag<(int I, double Count)>();
            var errors = (await batch.ProcessAsync(
                (i, count) => calls.Add((i, count)),
                new Settings(),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ToList();

            CollectionAssert.AreEquivalent(batch.Files.ToArray(), attempts.ToArray());
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("failed.jpg", errors[0].File);
            Assert.AreEqual("bad", errors[0].Error);
            AssertProgress(calls, fileCount);
        }

        [TestMethod]
        public async Task ProcessEmptyBatchDoesNotExecuteOrReportProgress()
        {
            var attempts = 0;
            var reports = 0;
            var batch = CreateBatch(_ => Interlocked.Increment(ref attempts));

            var errors = await batch.ProcessAsync(
                (_, __) => Interlocked.Increment(ref reports),
                new Settings(),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, errors.Count());
            Assert.AreEqual(0, attempts);
            Assert.AreEqual(0, reports);
        }

        [TestMethod]
        public async Task ProcessPrecancelledBatchDoesNotExecuteOrReportProgress()
        {
            var attempts = 0;
            var reports = 0;
            var batch = CreateBatch(_ => Interlocked.Increment(ref attempts));
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");
            batch.Files.Add("Image3.jpg");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var processing = batch.ProcessAsync(
                (_, __) => Interlocked.Increment(ref reports),
                new Settings(),
                cancellation.Token);
            try
            {
                await processing.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
            }

            Assert.IsTrue(processing.IsCanceled);
            Assert.AreEqual(0, attempts);
            Assert.AreEqual(0, reports);
        }

        [TestMethod]
        public async Task ProcessCancellationAllowsStartedOperationToComplete()
        {
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = 0;
            var batch = CreateBatch(async (_, __) =>
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Interlocked.Increment(ref completed);
            });
            batch.Files.Add("Image1.jpg");
            batch.Files.Add("Image2.jpg");
            batch.Files.Add("Image3.jpg");
            using var cancellation = new CancellationTokenSource();
            var processing = batch.ProcessAsync((_, __) => { }, new Settings(), cancellation.Token);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cancellation.Cancel();
            }
            finally
            {
                release.TrySetResult(true);
            }

            try
            {
                await processing.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
            }

            Assert.IsTrue(processing.IsCanceled);
            Assert.IsTrue(completed >= 1);
        }

        [TestMethod]
        public async Task ProcessProgressCallbackErrorFaultsBatch()
        {
            var attempts = 0;
            var batch = CreateBatch(_ => Interlocked.Increment(ref attempts));
            batch.Files.Add("Image1.jpg");
            var expected = new InvalidOperationException("progress");
            var processing = batch.ProcessAsync((_, __) => throw expected, new Settings(), CancellationToken.None);

            var actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await processing.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.AreSame(expected, actual);
            Assert.IsTrue(processing.IsFaulted);
            Assert.AreEqual(1, attempts);
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_ReportsMissingAndUnsupportedInputs()
        {
            using var directory = new TestDirectory();
            var unsupported = Path.Combine(directory, "notes.txt");
            var missing = Path.Combine(directory, "missing.png");
            File.WriteAllText(unsupported, "not an image");
            var options = Options(unsupported, missing);

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            Assert.AreEqual(0, batch.Files.Count);
            Assert.AreEqual(2, batch.InputErrors.Count);
            CollectionAssert.AreEquivalent(
                new[] { unsupported, missing },
                batch.InputErrors.Select(error => error.File).ToArray());
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_ResolvesWildcardMatches()
        {
            using var directory = new TestDirectory();
            var second = CopyTestImage(directory, "second.jpg");
            var first = CopyTestImage(directory, "first.jpg");
            var options = Options(Path.Combine(directory, "*.jpg"));

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            Assert.AreEqual(0, batch.InputErrors.Count);
            CollectionAssert.AreEqual(new[] { first, second }, batch.Files.ToArray());
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_DeduplicatesExplicitAndOverlappingWildcardPaths()
        {
            using var directory = new TestDirectory();
            var file = CopyTestImage(directory, "overlap.jpg");
            var options = Options(file, file.ToUpperInvariant(), Path.Combine(directory, "*.jpg"));

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            Assert.AreEqual(0, batch.InputErrors.Count);
            CollectionAssert.AreEqual(new[] { file }, batch.Files.ToArray());
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_DeduplicatesExtendedPathAlias()
        {
            using var directory = new TestDirectory();
            var file = CopyTestImage(directory, "alias.jpg");
            var options = Options(file, ToExtendedPath(file));

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            Assert.AreEqual(0, batch.InputErrors.Count);
            CollectionAssert.AreEqual(new[] { file }, batch.Files.ToArray());
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_DeduplicatesLongNormalAndExtendedPathAliases()
        {
            using var directory = new TestDirectory();
            var longDirectory = Path.Combine(directory, new string('a', 120), new string('b', 120));
            Directory.CreateDirectory(longDirectory);
            var sourceDirectory = Path.GetDirectoryName(typeof(ResizeBatchTests).Assembly.Location);
            var file = Path.Combine(longDirectory, "long-path.jpg");
            File.Copy(Path.Combine(sourceDirectory, "Test.jpg"), file);
            Assert.IsTrue(file.Length > 260);
            var normalAlias = file.ToUpperInvariant();

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(
                null,
                Options(normalAlias, ToExtendedPath(file)));

            Assert.AreEqual(0, batch.InputErrors.Count);
            CollectionAssert.AreEqual(new[] { normalAlias }, batch.Files.ToArray());
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_PreservesCaseOnlyFilesInCaseSensitiveDirectory()
        {
            using var directory = new TestDirectory();
            if (!TryEnableCaseSensitivity(directory, out var errorCode))
            {
                if (errorCode is 1 or 50 or 87)
                {
                    Assert.Inconclusive($"The test filesystem does not support per-directory case sensitivity (Win32 error {errorCode}).");
                }

                Assert.Fail($"Failed to enable per-directory case sensitivity (Win32 error {errorCode}).");
            }

            var first = CopyTestImage(directory, "Photo.jpg");
            var second = CopyTestImage(directory, "photo.jpg");

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, Options(first, second));

            Assert.AreEqual(0, batch.InputErrors.Count);
            CollectionAssert.AreEquivalent(new[] { first, second }, batch.Files.ToArray());
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task FromCliOptionsWithDiagnostics_NamedPipeReportsInvalidInputsAndDeduplicatesValidFiles()
        {
            using var directory = new TestDirectory();
            var valid = CopyTestImage(directory, "画像 pipe.JpG");
            var unsupported = Path.Combine(directory, "notes.txt");
            var missing = Path.Combine(directory, "missing.jpg");
            File.WriteAllText(unsupported, "not an image");
            var pipeName = $"ImageResizer-{Guid.NewGuid():N}";
            using var pipe = CreatePipeServer(pipeName);
            var writeTask = WritePipeLinesAsync(pipe, ToExtendedPath(valid), unsupported, missing);
            var options = Options(valid);
            options.PipeName = pipeName;

            ResizeBatch batch;
            try
            {
                batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);
            }
            finally
            {
                await writeTask.WaitAsync(TimeSpan.FromSeconds(10));
            }

            CollectionAssert.AreEqual(new[] { valid }, batch.Files.ToArray());
            Assert.AreEqual(2, batch.InputErrors.Count);
            CollectionAssert.AreEqual(
                new[] { unsupported, missing },
                batch.InputErrors.Select(error => error.File).ToArray());
            Assert.AreEqual(Resources.CLI_ErrorUnsupportedFileType, batch.InputErrors[0].Error);
            Assert.AreEqual(Resources.CLI_ErrorFileNotFound, batch.InputErrors[1].Error);
        }

        [TestMethod]
        [DataRow(1, false)]
        [DataRow(3, false)]
        [DataRow(3, true)]
        [Timeout(10000)]
        public async Task FromCliOptions_NamedPipeRemainsLenient(int fileCount, bool duplicateInputs)
        {
            using var directory = new TestDirectory();
            var valid = Enumerable.Range(1, fileCount)
                .Select(index => CopyTestImage(directory, $"画像 {index}.JpG"))
                .ToArray();
            var unsupported = Path.Combine(directory, "notes.txt");
            var missing = Path.Combine(directory, "missing.jpg");
            File.WriteAllText(unsupported, "not an image");
            var pipeName = $"ImageResizer-{Guid.NewGuid():N}";
            using var pipe = CreatePipeServer(pipeName);
            var lines = new[] { valid[0], unsupported, missing }.Concat(valid.Skip(1)).ToArray();
            if (duplicateInputs)
            {
                lines = lines.Append(valid[0]).ToArray();
            }

            var writeTask = WritePipeLinesAsync(pipe, lines);
            var options = Options();
            options.PipeName = pipeName;
            options.DestinationDirectory = Path.Combine(directory, "出力 folder");
            Directory.CreateDirectory(options.DestinationDirectory);
            if (duplicateInputs)
            {
                options.Files.Add(valid[0]);
            }

            ResizeBatch batch;
            try
            {
                batch = ResizeBatch.FromCliOptions(null, options);
            }
            finally
            {
                await writeTask.WaitAsync(TimeSpan.FromSeconds(10));
            }

            var expected = duplicateInputs ? new[] { valid[0] }.Concat(valid).Append(valid[0]).ToArray() : valid;
            CollectionAssert.AreEqual(expected, batch.Files.ToArray());
            Assert.AreEqual(options.DestinationDirectory, batch.DestinationDirectory);
            Assert.AreEqual(0, batch.InputErrors.Count);
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_ReportsWildcardWithNoMatches()
        {
            using var directory = new TestDirectory();
            var pattern = Path.Combine(directory, "*.jpg");
            var options = Options(pattern);

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            Assert.AreEqual(0, batch.Files.Count);
            Assert.AreEqual(1, batch.InputErrors.Count);
            Assert.AreEqual(pattern, batch.InputErrors[0].File);
        }

        [TestMethod]
        public void FromCliOptionsWithDiagnostics_PreservesValidFilesInMixedInput()
        {
            using var directory = new TestDirectory();
            var valid = CopyTestImage(directory, "valid.jpg");
            var missing = Path.Combine(directory, "missing.jpg");
            var options = Options(valid, missing);

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            CollectionAssert.AreEqual(new[] { valid }, batch.Files.ToArray());
            Assert.AreEqual(1, batch.InputErrors.Count);
            Assert.AreEqual(missing, batch.InputErrors[0].File);
        }

        [TestMethod]
        public void FromCliOptions_RemainsLenientForInvalidInputs()
        {
            using var directory = new TestDirectory();
            var options = Options(Path.Combine(directory, "missing.jpg"));

            var batch = ResizeBatch.FromCliOptions(null, options);

            Assert.AreEqual(0, batch.Files.Count);
            Assert.AreEqual(0, batch.InputErrors.Count);
        }

        [TestMethod]
        public async Task ProcessIncludesStrictInputDiagnostics()
        {
            using var directory = new TestDirectory();
            var firstMissing = Path.Combine(directory, "first-missing.jpg");
            var secondMissing = Path.Combine(directory, "second-missing.jpg");
            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, Options(firstMissing, secondMissing));

            var errors = (await batch.ProcessAsync((_, __) => { }, CancellationToken.None)).ToList();

            Assert.AreEqual(2, errors.Count);
            CollectionAssert.AreEqual(new[] { firstMissing, secondMissing }, errors.Select(error => error.File).ToArray());
        }

        [TestMethod]
        public void FormatErrorMessage_PreservesNonEmptyMessage()
        {
            const string message = "Decoder failed.";

            var result = ResizeBatch.FormatErrorMessage(new InvalidOperationException(message));

            Assert.AreEqual(message, result);
        }

        [TestMethod]
        public void FormatErrorMessage_UsesTypeAndHResultWhenMessageIsEmpty()
        {
            var result = ResizeBatch.FormatErrorMessage(new EmptyMessageException());

            StringAssert.Contains(result, nameof(EmptyMessageException));
            StringAssert.Contains(result, "0x88982F60");
        }

        [TestMethod]
        public void FormatErrorMessage_UsesTypeAndHResultWhenMessageIsWhitespace()
        {
            var result = ResizeBatch.FormatErrorMessage(new EmptyMessageException(" \t\r\n"));

            StringAssert.Contains(result, nameof(EmptyMessageException));
            StringAssert.Contains(result, "0x88982F60");
        }

        private static void AssertProgress(ConcurrentBag<(int I, double Count)> calls, int fileCount)
        {
            Assert.AreEqual(fileCount, calls.Count);
            foreach (var call in calls)
            {
                Assert.AreEqual((double)fileCount, call.Count);
                Assert.IsTrue(call.I >= 1 && call.I <= fileCount);
            }

            Assert.IsTrue(calls.Any(call => call.I == fileCount));
        }

        private static ResizeBatch CreateBatch(Action<string> executeAction)
            => CreateBatch((file, _) =>
            {
                executeAction(file);
                return Task.CompletedTask;
            });

        private static ResizeBatch CreateBatch(Func<string, Settings, Task> executeAction)
        {
            var mock = new Mock<ResizeBatch> { CallBase = true };
            mock.Protected()
                .Setup<Task>("ExecuteAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<Settings>())
                .Returns((string file, Settings settings) => executeAction(file, settings));

            return mock.Object;
        }

        private static CliOptions Options(params string[] files)
        {
            var options = new CliOptions();
            foreach (var file in files)
            {
                options.Files.Add(file);
            }

            return options;
        }

        private static string CopyTestImage(TestDirectory directory, string fileName, string sourceFileName = "Test.jpg")
        {
            var sourceDirectory = Path.GetDirectoryName(typeof(ResizeBatchTests).Assembly.Location);
            var destination = Path.Combine(directory, fileName);
            File.Copy(Path.Combine(sourceDirectory, sourceFileName), destination);
            return destination;
        }

        private static string ToExtendedPath(string path)
            => path.StartsWith(@"\\", StringComparison.Ordinal)
                ? string.Concat(@"\\?\UNC\", path.AsSpan(2))
                : @"\\?\" + path;

        private static NamedPipeServerStream CreatePipeServer(string pipeName)
            => new NamedPipeServerStream(
                pipeName,
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

        private static async Task WritePipeLinesAsync(NamedPipeServerStream pipe, params string[] lines)
        {
            await pipe.WaitForConnectionAsync().ConfigureAwait(false);

            // Shell writers send raw UTF-16LE paths without a byte-order mark.
            using var writer = new StreamWriter(pipe, new UnicodeEncoding(false, false)) { NewLine = "\r\n" };
            foreach (var line in lines)
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
        }

        private static bool TryEnableCaseSensitivity(string directory, out int errorCode)
        {
            const uint fileWriteAttributes = 0x00000100;
            const uint fileShareRead = 0x00000001;
            const uint fileShareWrite = 0x00000002;
            const uint fileShareDelete = 0x00000004;
            const uint openExisting = 3;
            const uint fileFlagBackupSemantics = 0x02000000;
            const uint fileCaseSensitiveDirectory = 0x00000001;

            using SafeFileHandle handle = CreateFile(
                directory,
                fileWriteAttributes,
                fileShareRead | fileShareWrite | fileShareDelete,
                IntPtr.Zero,
                openExisting,
                fileFlagBackupSemantics,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                errorCode = Marshal.GetLastWin32Error();
                return false;
            }

            var info = new FileCaseSensitiveInfo { Flags = fileCaseSensitiveDirectory };
            var result = SetFileInformationByHandle(
                handle,
                FileInfoByHandleClass.FileCaseSensitiveInfo,
                ref info,
                (uint)Marshal.SizeOf<FileCaseSensitiveInfo>());
            errorCode = result ? 0 : Marshal.GetLastWin32Error();
            return result;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(
            SafeFileHandle fileHandle,
            FileInfoByHandleClass fileInformationClass,
            ref FileCaseSensitiveInfo fileInformation,
            uint bufferSize);

        private sealed class EmptyMessageException : Exception
        {
            public EmptyMessageException(string message = "")
                : base(message)
            {
                HResult = unchecked((int)0x88982F60);
            }
        }

        private enum FileInfoByHandleClass
        {
            FileCaseSensitiveInfo = 23,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileCaseSensitiveInfo
        {
            public uint Flags;
        }
    }
}
