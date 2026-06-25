// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class NativeMethodsHelperTests
    {
        [TestMethod]
        public void GetCursorPos_after_SetCursorPos_round_trips()
        {
            // A safe coordinate inside any primary monitor.
            Assert.IsTrue(NativeMethodsHelper.SetCursorPos(100, 120));
            Assert.IsTrue(NativeMethodsHelper.GetCursorPos(out NativeMethodsHelper.POINT p));
            Assert.AreEqual(100, p.X);
            Assert.AreEqual(120, p.Y);
        }

        [TestMethod]
        public void POINT_implicit_operator_yields_Windows_Foundation_Point()
        {
            var p = new NativeMethodsHelper.POINT { X = 7, Y = 9 };
            Windows.Foundation.Point wf = p; // exercises the retargeted implicit operator
            Assert.AreEqual(7.0, wf.X);
            Assert.AreEqual(9.0, wf.Y);
        }
    }
}
