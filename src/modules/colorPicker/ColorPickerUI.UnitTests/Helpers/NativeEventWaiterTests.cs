// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class NativeEventWaiterTests
    {
        [TestMethod]
        public void WaitForEventLoop_without_a_dispatcher_queue_is_a_safe_no_op()
        {
            // MSTest worker thread has no DispatcherQueue: the guard logs + returns,
            // and signalling the event must NOT invoke the callback.
            var eventName = @"Local\PT_CP_Test_" + Guid.NewGuid().ToString("N");
            var fired = new ManualResetEventSlim(false);
            using var cts = new CancellationTokenSource();

            NativeEventWaiter.WaitForEventLoop(eventName, () => fired.Set(), cts.Token);

            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            signal.Set();

            Assert.IsFalse(fired.Wait(300), "callback must not fire when there is no DispatcherQueue to marshal onto");
        }
    }
}
