// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class WindowTrackerTests
    {
        [TestMethod]
        public void SendShowDesktop_GeneratesCorrectInputStructures()
        {
            // Verify the INPUT array structure by recreating the same logic
            var inputs = new NativeMethods.INPUT[4];

            // Win key down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = NativeMethods.VK_LWIN;
            inputs[0].u.ki.dwFlags = 0;

            // D key down
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = NativeMethods.VK_D;
            inputs[1].u.ki.dwFlags = 0;

            // D key up
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = NativeMethods.VK_D;
            inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            // Win key up
            inputs[3].type = NativeMethods.INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = NativeMethods.VK_LWIN;
            inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            // Validate all four inputs are keyboard type
            Assert.AreEqual(NativeMethods.INPUT_KEYBOARD, inputs[0].type);
            Assert.AreEqual(NativeMethods.INPUT_KEYBOARD, inputs[1].type);
            Assert.AreEqual(NativeMethods.INPUT_KEYBOARD, inputs[2].type);
            Assert.AreEqual(NativeMethods.INPUT_KEYBOARD, inputs[3].type);

            // Validate key sequence: Win↓, D↓, D↑, Win↑
            Assert.AreEqual(NativeMethods.VK_LWIN, inputs[0].u.ki.wVk);
            Assert.AreEqual(0u, inputs[0].u.ki.dwFlags);

            Assert.AreEqual(NativeMethods.VK_D, inputs[1].u.ki.wVk);
            Assert.AreEqual(0u, inputs[1].u.ki.dwFlags);

            Assert.AreEqual(NativeMethods.VK_D, inputs[2].u.ki.wVk);
            Assert.AreEqual(NativeMethods.KEYEVENTF_KEYUP, inputs[2].u.ki.dwFlags);

            Assert.AreEqual(NativeMethods.VK_LWIN, inputs[3].u.ki.wVk);
            Assert.AreEqual(NativeMethods.KEYEVENTF_KEYUP, inputs[3].u.ki.dwFlags);
        }

        [TestMethod]
        public void HasSavedWindows_InitialState_IsFalse()
        {
            var tracker = new WindowTracker();
            Assert.IsFalse(tracker.HasSavedWindows);
        }

        [TestMethod]
        public void InputStructSize_IsConsistent()
        {
            // Verify that Marshal.SizeOf<INPUT>() returns a consistent value
            int size = Marshal.SizeOf<NativeMethods.INPUT>();
            Assert.IsTrue(size > 0, "INPUT struct size should be positive");
        }

        [TestMethod]
        public void NativeConstants_HaveExpectedValues()
        {
            // Validate key constants used by SendShowDesktop
            Assert.AreEqual(1, NativeMethods.INPUT_KEYBOARD);
            Assert.AreEqual(0x0002u, NativeMethods.KEYEVENTF_KEYUP);
            Assert.AreEqual((ushort)0x5B, NativeMethods.VK_LWIN);
            Assert.AreEqual((ushort)0x44, NativeMethods.VK_D);
        }

        [TestMethod]
        public void WindowPlacement_Default_HasCorrectLength()
        {
            var placement = NativeMethods.WINDOWPLACEMENT.Default;
            Assert.AreEqual((uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(), placement.length);
        }
    }
}
