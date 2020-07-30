// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Interop.Tests
{
    [TestClass]
    public class InteropTests
    {
        private const string ServerSidePipe = "\\\\.\\pipe\\serverside";
        private const string ClientSidePipe = "\\\\.\\pipe\\clientside";

        private TwoWayPipeMessageIPCManaged clientPipe;

        [TestInitialize]
        public void Initialize()
        {
            clientPipe = new TwoWayPipeMessageIPCManaged(ClientSidePipe, ServerSidePipe, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            clientPipe.End();
        }

        [TestMethod]
        public void TestSend()
        {
            var testString = "This string is a test\n";
            var reset = new AutoResetEvent(false);

            var serverPipe = new TwoWayPipeMessageIPCManaged(
                ServerSidePipe,
                ClientSidePipe,
                (string msg) =>
                {
                    Assert.AreEqual(testString, msg);
                    reset.Set();
                });
            serverPipe.Start();
            clientPipe.Start();

            clientPipe.Send(testString);
            reset.WaitOne();

            serverPipe.End();
        }
    }
}
