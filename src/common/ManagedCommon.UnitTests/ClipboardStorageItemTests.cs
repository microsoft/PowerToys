// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Windows.Storage;

namespace ManagedCommon.UnitTests
{
    [TestClass]
    public sealed class ClipboardStorageItemTests
    {
        private static readonly string[] BlankPaths = { " " };
        private static readonly IStorageItem[] NullStorageItems = { null! };

        [TestMethod]
        public async Task TrySetAndGetFilePaths_RoundTripsFileAndFolder()
        {
            string root = CreateTestDirectory();
            string filePath = Path.Combine(root, "sample.txt");
            await File.WriteAllTextAsync(filePath, "sample");

            try
            {
                var backend = new TestClipboardBackend();
                using var executor = new ClipboardThreadExecutor();
                var service = CreateService(backend, executor);

                Assert.IsTrue(await service.TrySetFilePathsAsync(
                    new[] { filePath, root },
                    flush: true));

                Assert.IsTrue(service.TryGetFilePaths(out IReadOnlyList<string>? paths));
                CollectionAssert.AreEqual(new[] { filePath, root }, paths!.ToArray());
                Assert.AreEqual(1, backend.FlushCallCount);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public async Task TrySetAndGetStorageItems_RoundTripsWinRtItems()
        {
            string root = CreateTestDirectory();
            string filePath = Path.Combine(root, "sample.txt");
            await File.WriteAllTextAsync(filePath, "sample");

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(root);
                var backend = new TestClipboardBackend();
                using var executor = new ClipboardThreadExecutor();
                var service = CreateService(backend, executor);

                Assert.IsTrue(service.TrySetStorageItems(
                    new IStorageItem[] { file, folder },
                    flush: false));

                ClipboardReadResult<IReadOnlyList<IStorageItem>> result =
                    await service.TryGetStorageItemsAsync();

                Assert.IsTrue(result.Succeeded);
                CollectionAssert.AreEqual(
                    new[] { filePath, root },
                    result.Value!.Select(item => item.Path).ToArray());
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public async Task TrySetFilePathsAsync_NullBlankOrEmpty_DoesNotTouchBackend()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);

            Assert.IsFalse(await service.TrySetFilePathsAsync(null, flush: false));
            Assert.IsFalse(await service.TrySetFilePathsAsync(Array.Empty<string>(), flush: false));
            Assert.IsFalse(await service.TrySetFilePathsAsync(BlankPaths, flush: false));
            Assert.AreEqual(0, backend.SetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetStorageItemsAsync_EmptyOrNullItem_DoesNotTouchBackend()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);

            Assert.IsFalse(await service.TrySetStorageItemsAsync(
                Array.Empty<IStorageItem>(),
                flush: false));
            Assert.IsFalse(await service.TrySetStorageItemsAsync(
                NullStorageItems,
                flush: false));
            Assert.AreEqual(0, backend.SetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetFilePathsAsync_MissingPath_ThrowsFileNotFoundException()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            string missing = Path.Combine(
                AppContext.BaseDirectory,
                $"{Guid.NewGuid():N}",
                "missing.txt");

            FileNotFoundException exception = await Assert.ThrowsExceptionAsync<FileNotFoundException>(
                () => service.TrySetFilePathsAsync(new[] { missing }, flush: false));

            Assert.AreEqual(Path.GetFullPath(missing), exception.FileName);
            Assert.AreEqual(0, backend.SetContentCallCount);
        }

        private static ClipboardService CreateService(
            TestClipboardBackend backend,
            ClipboardThreadExecutor executor)
        {
            return new ClipboardService(backend, executor, retryDelayMilliseconds: 0);
        }

        private static string CreateTestDirectory()
        {
            string path = Path.Combine(
                AppContext.BaseDirectory,
                $"ClipboardStorageItemTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
