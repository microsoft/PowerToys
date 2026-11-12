// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class CoalescedActionTests
    {
        [TestMethod]
        public void Request_coalesces_until_the_queued_callback_runs()
        {
            Action? queuedCallback = null;
            int enqueueCount = 0;
            int actionCount = 0;
            var coalescedAction = new CoalescedAction(
                callback =>
                {
                    enqueueCount++;
                    queuedCallback = callback;
                    return true;
                },
                () => actionCount++);

            coalescedAction.Request();
            coalescedAction.Request();

            Assert.AreEqual(1, enqueueCount);
            Assert.AreEqual(0, actionCount);
            Assert.IsNotNull(queuedCallback);

            queuedCallback!();

            Assert.AreEqual(1, actionCount);
        }

        [TestMethod]
        public void Request_can_schedule_again_after_execution()
        {
            Action? queuedCallback = null;
            int enqueueCount = 0;
            var coalescedAction = new CoalescedAction(
                callback =>
                {
                    enqueueCount++;
                    queuedCallback = callback;
                    return true;
                },
                () => { });

            coalescedAction.Request();
            queuedCallback!();
            coalescedAction.Request();

            Assert.AreEqual(2, enqueueCount);
        }

        [TestMethod]
        public void Request_retries_after_queue_rejection()
        {
            int enqueueCount = 0;
            var coalescedAction = new CoalescedAction(
                callback =>
                {
                    enqueueCount++;
                    return false;
                },
                () => { });

            coalescedAction.Request();
            coalescedAction.Request();

            Assert.AreEqual(2, enqueueCount);
        }
    }
}
