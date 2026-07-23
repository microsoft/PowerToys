// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
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
        public async Task TrySetImageAsync_StreamCopiesMultipleBufferChunks()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            var imageBytes = new byte[100_000];
            for (int i = 0; i < imageBytes.Length; i++)
            {
                imageBytes[i] = (byte)(i % 251);
            }

            using var input = new MemoryStream(imageBytes);
            Assert.IsTrue(await service.TrySetImageAsync(input, flush: false));

            RandomAccessStreamReference reference = await backend.Content.GetView().GetBitmapAsync();
            CollectionAssert.AreEqual(imageBytes, await ReadBytesAsync(reference));
        }

        [TestMethod]
        public async Task TrySetImage_SyncOverAsync_DoesNotCaptureCallerSynchronizationContext()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            using var stream = new DeferredAsyncReadStream(PngBytes);
            var callerContext = new QueuedSynchronizationContext();
            using var started = new ManualResetEventSlim();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                SynchronizationContext? previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(callerContext);
                try
                {
                    started.Set();
                    completion.SetResult(service.TrySetImage(stream, flush: false));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }
            })
            {
                IsBackground = true,
            };

            thread.Start();
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(1)));

            if (!completion.Task.Wait(TimeSpan.FromMilliseconds(500)))
            {
                callerContext.DrainPostedCallbacks();
            }

            Assert.IsTrue(completion.Task.Wait(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(completion.Task.Result);
            Assert.AreEqual(0, callerContext.PostCount);
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));

            RandomAccessStreamReference reference = await backend.Content.GetView().GetBitmapAsync();
            CollectionAssert.AreEqual(PngBytes, await ReadBytesAsync(reference));
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

        [TestMethod]
        public async Task TrySetImageAsync_ConcurrentCalls_ReadSourcesSerially()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            using var firstInput = new ObservedAsyncReadStream(PngBytes, blockFirstRead: true);
            using var secondInput = new ObservedAsyncReadStream(PngBytes, blockFirstRead: false);

            Task<bool> firstWrite = service.TrySetImageAsync(firstInput, flush: false);
            Assert.IsTrue(firstInput.ReadStarted.Wait(TimeSpan.FromSeconds(1)));

            Task<bool> secondWrite = service.TrySetImageAsync(secondInput, flush: false);
            Assert.IsFalse(secondInput.ReadStarted.Wait(TimeSpan.FromMilliseconds(100)));

            firstInput.Release();

            Assert.IsTrue(await firstWrite);
            Assert.IsTrue(await secondWrite);
            Assert.IsTrue(secondInput.ReadStarted.IsSet);
        }

        [TestMethod]
        public async Task TrySetImageAsync_SetContentRetry_ReusesPreparedPackageAndLeavesInputOpen()
        {
            var backend = new TestClipboardBackend();
            backend.SetContentFailures.Enqueue(new UnauthorizedAccessException("busy"));
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            using var input = new ObservedAsyncReadStream(PngBytes, blockFirstRead: false);

            Assert.IsTrue(await service.TrySetImageAsync(input, flush: false));

            Assert.AreEqual(2, backend.SetContentCallCount);
            Assert.AreEqual(PngBytes.Length, input.TotalBytesRead);
            Assert.IsFalse(input.WasDisposed);
            RandomAccessStreamReference reference = await backend.Content.GetView().GetBitmapAsync();
            CollectionAssert.AreEqual(PngBytes, await ReadBytesAsync(reference));
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

        private sealed class DeferredAsyncReadStream : Stream
        {
            private readonly byte[] _bytes;
            private int _position;

            public DeferredAsyncReadStream(byte[] bytes)
            {
                _bytes = bytes;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int remaining = _bytes.Length - _position;
                int toCopy = Math.Min(remaining, count);
                if (toCopy <= 0)
                {
                    return 0;
                }

                Array.Copy(_bytes, _position, buffer, offset, toCopy);
                _position += toCopy;
                return toCopy;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                completion.TrySetCanceled(cancellationToken);
                                return;
                            }

                            completion.TrySetResult(Read(buffer.Span));
                        }
                        catch (Exception ex)
                        {
                            completion.TrySetException(ex);
                        }
                    });
                return new ValueTask<int>(completion.Task);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ObservedAsyncReadStream : Stream
        {
            private readonly byte[] _bytes;
            private readonly TaskCompletionSource<object?> _release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private int _position;
            private int _totalBytesRead;

            internal ObservedAsyncReadStream(byte[] bytes, bool blockFirstRead)
            {
                _bytes = bytes;
                if (!blockFirstRead)
                {
                    _release.SetResult(null);
                }
            }

            internal ManualResetEventSlim ReadStarted { get; } = new();

            internal int TotalBytesRead => Volatile.Read(ref _totalBytesRead);

            internal bool WasDisposed { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            internal void Release() => _release.TrySetResult(null);

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int remaining = _bytes.Length - _position;
                int toCopy = Math.Min(remaining, count);
                if (toCopy <= 0)
                {
                    return 0;
                }

                Array.Copy(_bytes, _position, buffer, offset, toCopy);
                _position += toCopy;
                Interlocked.Add(ref _totalBytesRead, toCopy);
                return toCopy;
            }

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                ReadStarted.Set();
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return Read(buffer.Span);
            }

            public override Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    WasDisposed = true;
                    _release.TrySetResult(null);
                    ReadStarted.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
            private int _postCount;

            public int PostCount => Volatile.Read(ref _postCount);

            public override void Post(SendOrPostCallback d, object? state)
            {
                Interlocked.Increment(ref _postCount);
                _callbacks.Enqueue((d, state));
            }

            public void DrainPostedCallbacks()
            {
                while (_callbacks.TryDequeue(out (SendOrPostCallback Callback, object? State) callback))
                {
                    using var finished = new ManualResetEventSlim();
                    Exception? callbackException = null;

                    ThreadPool.QueueUserWorkItem(
                        _ =>
                        {
                            SynchronizationContext? previous = Current;
                            try
                            {
                                SetSynchronizationContext(this);
                                callback.Callback(callback.State);
                            }
                            catch (Exception ex)
                            {
                                callbackException = ex;
                            }
                            finally
                            {
                                SetSynchronizationContext(previous);
                                finished.Set();
                            }
                        });

                    Assert.IsTrue(finished.Wait(TimeSpan.FromSeconds(2)));
                    if (callbackException is not null)
                    {
                        ExceptionDispatchInfo.Capture(callbackException).Throw();
                    }
                }
            }
        }
    }
}
