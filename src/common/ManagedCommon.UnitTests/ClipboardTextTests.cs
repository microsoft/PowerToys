// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace ManagedCommon.UnitTests
{
    [TestClass]
    public sealed class ClipboardTextTests
    {
        [TestMethod]
        public async Task TrySetTextAsync_NullOrEmpty_DoesNotTouchBackend()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);

            Assert.IsFalse(await service.TrySetTextAsync(null, flush: false));
            Assert.IsFalse(await service.TrySetTextAsync(string.Empty, flush: false));
            Assert.AreEqual(0, backend.SetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetText_SetsTextAndHonorsFlush()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);

            Assert.IsTrue(service.TrySetText("copied text", flush: true));

            DataPackageView view = backend.Content.GetView();
            Assert.AreEqual("copied text", await view.GetTextAsync());
            Assert.AreEqual(1, backend.FlushCallCount);
        }

        [TestMethod]
        public async Task TrySetRtfAsync_WritesOnlyRtf()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            const string Rtf = @"{\rtf1\ansi copied}";

            Assert.IsTrue(await service.TrySetRtfAsync(Rtf, flush: false));

            DataPackageView view = backend.Content.GetView();
            Assert.IsTrue(view.Contains(StandardDataFormats.Rtf));
            Assert.IsFalse(view.Contains(StandardDataFormats.Text));
            Assert.AreEqual(Rtf, await view.GetRtfAsync());
            Assert.AreEqual(0, backend.FlushCallCount);
        }

        [TestMethod]
        public async Task TryGetTextAndRtf_UseTheSameStoredPackage()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);
            backend.Content.SetText("plain");
            backend.Content.SetRtf(@"{\rtf1\ansi rich}");

            Assert.IsTrue(service.TryGetText(out string? text));
            ClipboardReadResult<string> rtf = await service.TryGetRtfAsync();

            Assert.AreEqual("plain", text);
            Assert.IsTrue(rtf.Succeeded);
            Assert.AreEqual(@"{\rtf1\ansi rich}", rtf.Value);
        }

        [TestMethod]
        public async Task TryGetTextAsync_MissingFormat_ReturnsFailure()
        {
            var backend = new TestClipboardBackend();
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor);

            ClipboardReadResult<string> result = await service.TryGetTextAsync();

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Value);
        }

        [TestMethod]
        public async Task TryGetTextAsync_RetriesTransientGetFailure()
        {
            var backend = new TestClipboardBackend();
            backend.Content.SetText("value");
            backend.GetContentFailures.Enqueue(new UnauthorizedAccessException("busy"));
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor, maxAttempts: 3);

            ClipboardReadResult<string> result = await service.TryGetTextAsync();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("value", result.Value);
            Assert.AreEqual(2, backend.GetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetTextAsync_RetriesTransientFailureUntilSuccess()
        {
            var backend = new TestClipboardBackend();
            backend.SetContentFailures.Enqueue(new UnauthorizedAccessException("busy"));
            backend.SetContentFailures.Enqueue(CreateComException("busy"));
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor, maxAttempts: 5);

            Assert.IsTrue(await service.TrySetTextAsync("value", flush: false));
            Assert.AreEqual(3, backend.SetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetTextAsync_TransientFailureExhaustion_ReturnsFalse()
        {
            var backend = new TestClipboardBackend();
            backend.SetContentFailures.Enqueue(CreateComException("busy"));
            backend.SetContentFailures.Enqueue(CreateComException("busy"));
            backend.SetContentFailures.Enqueue(CreateComException("busy"));
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor, maxAttempts: 3);

            Assert.IsFalse(await service.TrySetTextAsync("value", flush: false));
            Assert.AreEqual(3, backend.SetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetTextAsync_UnexpectedFailure_PropagatesImmediately()
        {
            var backend = new TestClipboardBackend();
            backend.SetContentFailures.Enqueue(new InvalidOperationException("unexpected"));
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor, maxAttempts: 5);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.TrySetTextAsync("value", flush: false));

            Assert.AreEqual(1, backend.SetContentCallCount);
        }

        [TestMethod]
        public async Task TrySetTextAsync_FlushFailure_RetriesSetAndFlush()
        {
            var backend = new TestClipboardBackend();
            backend.FlushFailures.Enqueue(CreateComException("not owner"));
            using var executor = new ClipboardThreadExecutor();
            var service = CreateService(backend, executor, maxAttempts: 3);

            Assert.IsTrue(await service.TrySetTextAsync("value", flush: true));
            Assert.AreEqual(2, backend.SetContentCallCount);
            Assert.AreEqual(2, backend.FlushCallCount);
        }

        private static ClipboardService CreateService(
            TestClipboardBackend backend,
            ClipboardThreadExecutor executor,
            int maxAttempts = 10)
        {
            return new ClipboardService(
                backend,
                executor,
                maxAttempts,
                retryDelayMilliseconds: 0);
        }

#pragma warning disable CA2201 // COMException is required to validate the transient clipboard retry policy.
        private static COMException CreateComException(string message)
        {
            return new COMException(message);
        }
#pragma warning restore CA2201 // COMException is required to validate the transient clipboard retry policy.
    }
}
