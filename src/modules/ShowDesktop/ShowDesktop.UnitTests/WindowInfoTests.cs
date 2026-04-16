// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class WindowInfoTests
    {
        [TestMethod]
        public void Constructor_StoresHwnd()
        {
            IntPtr hwnd = new IntPtr(12345);
            var placement = NativeMethods.WINDOWPLACEMENT.Default;

            var info = new WindowInfo(hwnd, placement);

            Assert.AreEqual(hwnd, info.Hwnd);
        }

        [TestMethod]
        public void Constructor_StoresPlacement()
        {
            IntPtr hwnd = new IntPtr(42);
            var placement = NativeMethods.WINDOWPLACEMENT.Default;
            placement.showCmd = NativeMethods.SW_SHOWMAXIMIZED;

            var info = new WindowInfo(hwnd, placement);

            Assert.AreEqual((uint)NativeMethods.SW_SHOWMAXIMIZED, info.Placement.showCmd);
        }

        [TestMethod]
        public void Constructor_ZeroHwnd_IsAllowed()
        {
            var placement = NativeMethods.WINDOWPLACEMENT.Default;
            var info = new WindowInfo(IntPtr.Zero, placement);

            Assert.AreEqual(IntPtr.Zero, info.Hwnd);
        }

        [TestMethod]
        public void Placement_PreservesNormalPosition()
        {
            var placement = NativeMethods.WINDOWPLACEMENT.Default;
            placement.rcNormalPosition = new NativeMethods.RECT
            {
                Left = 10,
                Top = 20,
                Right = 810,
                Bottom = 620,
            };

            var info = new WindowInfo(new IntPtr(1), placement);

            Assert.AreEqual(10, info.Placement.rcNormalPosition.Left);
            Assert.AreEqual(20, info.Placement.rcNormalPosition.Top);
            Assert.AreEqual(810, info.Placement.rcNormalPosition.Right);
            Assert.AreEqual(620, info.Placement.rcNormalPosition.Bottom);
        }
    }
}
