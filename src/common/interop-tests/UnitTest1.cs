using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Pipes;
using interop;
using System.IO;
using System.Threading;
using System.Text;

namespace interop_tests
{
    [TestClass]
    public class UnitTest1
    {
        private string SERVER_SIDE = "\\\\.\\pipe\\serverside";
        private string CLIENT_SIDE = "\\\\.\\pipe\\clientside";

        private TwoWayPipeMessageIPCManaged clientPipe;

        [TestInitialize]
        public void Initialize()
        {
            clientPipe = new TwoWayPipeMessageIPCManaged(CLIENT_SIDE, SERVER_SIDE, null);
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
                SERVER_SIDE,
                CLIENT_SIDE,
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
