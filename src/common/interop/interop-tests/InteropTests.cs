// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Interop.Tests
{
    [TestClass]
    public class InteropTests : IDisposable
    {
        private const string ServerSidePipe = "\\\\.\\pipe\\serverside";
        private const string ClientSidePipe = "\\\\.\\pipe\\clientside";

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
                    reset.WaitOne();

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
