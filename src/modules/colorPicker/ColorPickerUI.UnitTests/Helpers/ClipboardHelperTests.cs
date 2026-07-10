// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class ClipboardHelperTests
    {
        [TestMethod]
        public void CopyToClipboard_null_is_a_no_op()
        {
            ClipboardHelper.CopyToClipboard(null); // must not throw, must not touch the clipboard
        }

        [TestMethod]
        public void CopyToClipboard_empty_is_a_no_op()
        {
            ClipboardHelper.CopyToClipboard(string.Empty); // must not throw
        }

        [TestMethod]
        public void TrySetClipboard_retries_UnauthorizedAccessException_until_success()
        {
            int calls = 0;
            bool result = ClipboardHelper.TrySetClipboard(
                () =>
                {
                    calls++;
                    if (calls < 3)
                    {
                        throw new UnauthorizedAccessException("busy");
                    }
                },
                maxAttempts: 5,
                delayMs: 0,
                out Exception? lastException);

            Assert.IsTrue(result);
            Assert.AreEqual(3, calls);
            Assert.IsNull(lastException);
        }

        [TestMethod]
        public void TrySetClipboard_returns_false_and_exposes_last_exception_after_max_attempts()
        {
            int calls = 0;
            bool result = ClipboardHelper.TrySetClipboard(
                () =>
                {
                    calls++;
                    throw new UnauthorizedAccessException("locked");
                },
                maxAttempts: 3,
                delayMs: 0,
                out Exception? lastException);

            Assert.IsFalse(result);
            Assert.AreEqual(3, calls);
            Assert.IsInstanceOfType(lastException, typeof(UnauthorizedAccessException));
        }

        [TestMethod]
        public void TrySetClipboard_propagates_unexpected_exception_immediately()
        {
            int calls = 0;
            Exception? captured = null;

            try
            {
                _ = ClipboardHelper.TrySetClipboard(
                    () =>
                    {
                        calls++;
                        throw new InvalidOperationException("unexpected");
                    },
                    maxAttempts: 5,
                    delayMs: 0,
                    out captured);
                Assert.Fail("Expected InvalidOperationException to propagate");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.AreEqual(1, calls, "unexpected exception must propagate after exactly one attempt");
        }
    }
}
