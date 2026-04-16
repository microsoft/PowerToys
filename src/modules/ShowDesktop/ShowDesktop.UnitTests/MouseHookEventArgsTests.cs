// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class MouseHookEventArgsTests
    {
        [TestMethod]
        public void Properties_DefaultValues_AreCorrect()
        {
            var args = new MouseHookEventArgs();

            Assert.AreEqual(0, args.X);
            Assert.AreEqual(0, args.Y);
            Assert.IsFalse(args.IsDoubleClick);
            Assert.IsFalse(args.IsTaskbar);
        }

        [TestMethod]
        public void Properties_WhenInitialized_ReturnSetValues()
        {
            var args = new MouseHookEventArgs
            {
                X = 100,
                Y = 200,
                IsDoubleClick = true,
                IsTaskbar = false,
            };

            Assert.AreEqual(100, args.X);
            Assert.AreEqual(200, args.Y);
            Assert.IsTrue(args.IsDoubleClick);
            Assert.IsFalse(args.IsTaskbar);
        }

        [TestMethod]
        public void Properties_TaskbarClick_SetsCorrectly()
        {
            var args = new MouseHookEventArgs
            {
                X = 500,
                Y = 1060,
                IsDoubleClick = false,
                IsTaskbar = true,
            };

            Assert.AreEqual(500, args.X);
            Assert.AreEqual(1060, args.Y);
            Assert.IsFalse(args.IsDoubleClick);
            Assert.IsTrue(args.IsTaskbar);
        }

        [TestMethod]
        public void Properties_NegativeCoordinates_AreAllowed()
        {
            var args = new MouseHookEventArgs
            {
                X = -100,
                Y = -200,
            };

            Assert.AreEqual(-100, args.X);
            Assert.AreEqual(-200, args.Y);
        }
    }
}
