// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Windows.Storage.Streams;

namespace ManagedCommon.UnitTests
{
    [TestClass]
    public sealed class ClipboardImageTests
    {
        private static readonly byte[] PngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

        [TestMethod]
        public async Task TrySetImageAsync_StreamCopiesEncodedBytes()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            byte[] prefixedBytes = { 0x00, 0x01, 0x02 };
            using var input = new MemoryStream();
            await input.WriteAsync(prefixedBytes.AsMemory());
            await input.WriteAsync(PngBytes.AsMemory());
            input.Position = prefixedBytes.Length;

            Assert.IsTrue(await service.TrySetImageAsync(input, flush: true));
            input.Dispose();

            RandomAccessStreamReference reference = await backend.Content.GetView().GetBitmapAsync();
            CollectionAssert.AreEqual(PngBytes, await ReadBytesAsync(reference));
            Assert.AreEqual(1, backend.FlushCallCount);
        }

        [TestMethod]
        public async Task TryGetImageStreamAsync_ReturnsIndependentStreamAtPositionZero()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            using InMemoryRandomAccessStream source = await CreateRandomAccessStreamAsync(PngBytes);
            backend.Content.SetBitmap(RandomAccessStreamReference.CreateFromStream(source));

            ClipboardReadResult<Stream> result = await service.TryGetImageStreamAsync();

            Assert.IsTrue(result.Succeeded);
            Stream resultStream = result.Value ?? throw new AssertFailedException("Expected an image stream.");
            Assert.AreEqual(0, resultStream.Position);
            using (resultStream)
            {
                using var copy = new MemoryStream();
                await resultStream.CopyToAsync(copy);
                CollectionAssert.AreEqual(PngBytes, copy.ToArray());
            }
        }

        [TestMethod]
        public async Task TrySetAndGetImage_WinRtReferenceRoundTrips()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            using InMemoryRandomAccessStream source = await CreateRandomAccessStreamAsync(PngBytes);
            RandomAccessStreamReference reference = RandomAccessStreamReference.CreateFromStream(source);

            Assert.IsTrue(service.TrySetImage(reference, flush: false));
            Assert.IsTrue(service.TryGetImage(out RandomAccessStreamReference? returnedReference));
            Assert.IsNotNull(returnedReference);
            CollectionAssert.AreEqual(PngBytes, await ReadBytesAsync(returnedReference!));
        }

        [TestMethod]
        public async Task TrySetImageAsync_EmptyStream_ReturnsFalseWithoutBackendAccess()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            using var input = new MemoryStream();

            Assert.IsFalse(await service.TrySetImageAsync(input, flush: false));
            Assert.AreEqual(0, backend.SetContentCallCount);
        }

        private static ClipboardService CreateService(
            TestClipboardBackend backend,
            ClipboardThreadExecutor executor)
        {
            return new ClipboardService(backend, executor, retryDelayMilliseconds: 0);
        }

        private static async Task<InMemoryRandomAccessStream> CreateRandomAccessStreamAsync(byte[] bytes)
        {
            var stream = new InMemoryRandomAccessStream();
            using var input = new MemoryStream(bytes);
            using IInputStream inputStream = input.AsInputStream();
            await RandomAccessStream.CopyAsync(inputStream, stream);
            stream.Seek(0);
            return stream;
        }

        private static async Task<byte[]> ReadBytesAsync(RandomAccessStreamReference reference)
        {
            using IRandomAccessStreamWithContentType source = await reference.OpenReadAsync();
            using Stream sourceStream = source.AsStreamForRead();
            using var output = new MemoryStream();
            await sourceStream.CopyToAsync(output);
            return output.ToArray();
        }
    }
}
