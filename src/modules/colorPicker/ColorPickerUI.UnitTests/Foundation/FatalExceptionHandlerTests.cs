// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ColorPicker.Foundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Foundation
{
    [TestClass]
    public class FatalExceptionHandlerTests
    {
        [TestMethod]
        public void Handle_InvokesLogAndRestoreOnlyOnce()
        {
            int logCount = 0;
            int restoreCount = 0;
            var handler = new FatalExceptionHandler(_ => logCount++, () => restoreCount++);
            var exception = new InvalidOperationException("boom");

            handler.Handle(exception);
            handler.Handle(exception);

            Assert.AreEqual(1, logCount);
            Assert.AreEqual(1, restoreCount);
        }

        [TestMethod]
        public void Handle_ForwardsTheOriginalExceptionToTheLogger()
        {
            Exception? loggedException = null;
            var handler = new FatalExceptionHandler(ex => loggedException = ex, () => { });
            var exception = new InvalidOperationException("boom");

            handler.Handle(exception);

            Assert.AreSame(exception, loggedException);
        }
    }
}
