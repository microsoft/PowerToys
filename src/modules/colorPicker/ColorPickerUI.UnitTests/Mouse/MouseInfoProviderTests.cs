// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

using ColorPicker.Mouse;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Mouse
{
    /// <summary>
    /// Unit tests for <see cref="MouseInfoProvider.TryGetPixelColor"/>.
    /// These tests exercise the delegate-testable capture helper directly — no mock framework,
    /// no UI construction required.
    /// </summary>
    [TestClass]
    public class MouseInfoProviderTests
    {
        [TestMethod]
        public void TryGetPixelColor_success_returns_true_and_exact_color()
        {
            var expected = Color.FromArgb(12, 34, 56, 78);

            bool ok = MouseInfoProvider.TryGetPixelColor(() => expected, out Color actual);

            Assert.IsTrue(ok);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TryGetPixelColor_Win32Exception_returns_false_and_transparent()
        {
            // Win32Exception is thrown by Graphics.CopyFromScreen when the desktop
            // context is unavailable (error "the handle is invalid").
            bool ok = MouseInfoProvider.TryGetPixelColor(
                () => throw new Win32Exception(0, "simulated GDI failure"),
                out Color actual);

            Assert.IsFalse(ok);
            Assert.AreEqual(Color.Transparent, actual);
        }

        [TestMethod]
        public void TryGetPixelColor_ExternalException_returns_false_and_transparent()
        {
            // Use a private concrete subclass so CA2201 is satisfied while still exercising
            // the ExternalException catch branch (Win32Exception would hit the earlier branch).
            bool ok = MouseInfoProvider.TryGetPixelColor(
                () => throw new GdiPlusStatusException("simulated external failure"),
                out Color actual);

            Assert.IsFalse(ok);
            Assert.AreEqual(Color.Transparent, actual);
        }

        // Private ExternalException subclass used to exercise the ExternalException catch branch
        // without triggering CA2201 (which rejects throwing the reserved base type directly).
        private sealed class GdiPlusStatusException : ExternalException
        {
            public GdiPlusStatusException(string message)
                : base(message)
            {
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TryGetPixelColor_unexpected_exception_propagates()
        {
            _ = MouseInfoProvider.TryGetPixelColor(
                () => throw new InvalidOperationException("unexpected – must not be caught"),
                out _);
        }
    }
}
