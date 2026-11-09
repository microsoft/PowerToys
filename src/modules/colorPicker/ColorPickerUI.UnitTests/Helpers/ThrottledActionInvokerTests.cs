// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class ThrottledActionInvokerTests
    {
        [TestMethod]
        public void Ctor_throws_without_a_dispatcher_queue()
        {
            // MSTest runs on a worker thread that has no DispatcherQueue, so the
            // ctor's GetForCurrentThread() guard must throw. This also proves the
            // timer is bound at construction time (not lazily).
            Assert.ThrowsException<InvalidOperationException>(() => new ThrottledActionInvoker());
        }
    }
}
