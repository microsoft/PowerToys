// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Interop;

namespace Microsoft.Interop.Tests
{
    [TestClass]
    public class InteropTests : IDisposable
    {
        // Pipe names are machine-global, so two concurrent test runs on the same CI agent
        // (or a leaked handle from a prior run) would deadlock if we used a shared constant.
        // Suffix with process id + a GUID so every test run gets its own pair.
        private const string PipePrefix = @"\\.\pipe\";
        private static readonly string PipeSuffix = $"{Environment.ProcessId}_{Guid.NewGuid():N}";
        private static readonly string ServerSidePipe = $"{PipePrefix}serverside_{PipeSuffix}";
        private static readonly string ClientSidePipe = $"{PipePrefix}clientside_{PipeSuffix}";

        private static readonly TimeSpan MessageWaitTimeout = TimeSpan.FromSeconds(30);

        internal TwoWayPipeMessageIPCManaged ClientPipe { get; set; }

        private bool disposedValue;

        [TestInitialize]
        public void Initialize()
        {
            ClientPipe = new TwoWayPipeMessageIPCManaged(ClientSidePipe, ServerSidePipe, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClientPipe.End();
        }

        [TestMethod]
        public void TestSend()
        {
            var testString = "This string is a test\n";
            using (var reset = new AutoResetEvent(false))
            {
                using (var serverPipe = new TwoWayPipeMessageIPCManaged(
                    ServerSidePipe,
                    ClientSidePipe,
                    (string msg) =>
                    {
                        Assert.AreEqual(testString, msg);
                        reset.Set();
                    }))
                {
                    serverPipe.Start();
                    ClientPipe.Start();

                    // Test can be flaky as the pipes are still being set up and we end up receiving no message. Wait for a bit to avoid that.
                    Thread.Sleep(100);

                    ClientPipe.Send(testString);

                    // Bounded wait so a broken pipe handshake fails the test quickly
                    // instead of hanging the CI agent until the job-level timeout.
                    var timeoutMessage = $"Pipe callback was not invoked within {MessageWaitTimeout.TotalSeconds}s. Server='{ServerSidePipe}' Client='{ClientSidePipe}'.";
                    Assert.IsTrue(reset.WaitOne(MessageWaitTimeout), timeoutMessage);

                    serverPipe.End();
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ClientPipe.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
