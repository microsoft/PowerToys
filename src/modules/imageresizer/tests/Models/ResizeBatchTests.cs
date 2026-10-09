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
            var first = CopyTestImage(directory, "first.jpg");
            var second = CopyTestImage(directory, "second.jpg");
            var options = Options(Path.Combine(directory, "*.jpg"));

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);

            Assert.AreEqual(0, batch.InputErrors.Count);
            CollectionAssert.AreEquivalent(new[] { first, second }, batch.Files.ToArray());
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
            var valid = CopyTestImage(directory, "pipe.jpg");
            var unsupported = Path.Combine(directory, "notes.txt");
            var missing = Path.Combine(directory, "missing.jpg");
            File.WriteAllText(unsupported, "not an image");
            var pipeName = $"ImageResizer-{Guid.NewGuid():N}";
            using var pipe = CreatePipeServer(pipeName);
            var writeTask = WritePipeLinesAsync(pipe, ToExtendedPath(valid), unsupported, missing);
            var options = Options(valid);
            options.PipeName = pipeName;

            var batch = ResizeBatch.FromCliOptionsWithDiagnostics(null, options);
            await writeTask;

            CollectionAssert.AreEqual(new[] { valid }, batch.Files.ToArray());
            CollectionAssert.AreEquivalent(
                new[] { unsupported, missing },
                batch.InputErrors.Select(error => error.File).ToArray());
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task FromCliOptions_NamedPipeRemainsLenient()
        {
            using var directory = new TestDirectory();
            var valid = CopyTestImage(directory, "pipe.jpg");
            var missing = Path.Combine(directory, "missing.jpg");
            var pipeName = $"ImageResizer-{Guid.NewGuid():N}";
            using var pipe = CreatePipeServer(pipeName);
            var writeTask = WritePipeLinesAsync(pipe, valid, missing);
            var options = Options();
            options.PipeName = pipeName;

            var batch = ResizeBatch.FromCliOptions(null, options);
            await writeTask;

            CollectionAssert.AreEqual(new[] { valid }, batch.Files.ToArray());
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

        private static CliOptions Options(params string[] files)
        {
            var options = new CliOptions();
            foreach (var file in files)
            {
                options.Files.Add(file);
            }

            return options;
        }

        private static string CopyTestImage(TestDirectory directory, string fileName)
        {
            var sourceDirectory = Path.GetDirectoryName(typeof(ResizeBatchTests).Assembly.Location);
            var destination = Path.Combine(directory, fileName);
            File.Copy(Path.Combine(sourceDirectory, "Test.jpg"), destination);
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
            using var writer = new StreamWriter(pipe, Encoding.Unicode);
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
            public EmptyMessageException()
                : base(string.Empty)
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
