// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for PositionWrapper struct equality and operator behavior.
    /// </summary>
    [TestClass]
    public class WindowPositionDataTests
    {
        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquality_IdenticalCoordinates_ReturnsTrue()
        {
            var pos1 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            var pos2 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            Assert.IsTrue(pos1 == pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquality_DifferentXCoordinate_ReturnsFalse()
        {
            var pos1 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            var pos2 = new PositionWrapper { X = 101, Y = 200, Width = 800, Height = 600 };
            Assert.IsFalse(pos1 == pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquality_DifferentYCoordinate_ReturnsFalse()
        {
            var pos1 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            var pos2 = new PositionWrapper { X = 100, Y = 201, Width = 800, Height = 600 };
            Assert.IsFalse(pos1 == pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquality_DifferentWidth_ReturnsFalse()
        {
            var pos1 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            var pos2 = new PositionWrapper { X = 100, Y = 200, Width = 801, Height = 600 };
            Assert.IsFalse(pos1 == pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquality_DifferentHeight_ReturnsFalse()
        {
            var pos1 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            var pos2 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 601 };
            Assert.IsFalse(pos1 == pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionInequality_DifferentCoordinates_ReturnsTrue()
        {
            var pos1 = new PositionWrapper { X = 0, Y = 0, Width = 1920, Height = 1080 };
            var pos2 = new PositionWrapper { X = 960, Y = 0, Width = 960, Height = 1080 };
            Assert.IsTrue(pos1 != pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionInequality_IdenticalCoordinates_ReturnsFalse()
        {
            var pos1 = new PositionWrapper { X = 0, Y = 0, Width = 1920, Height = 1080 };
            var pos2 = new PositionWrapper { X = 0, Y = 0, Width = 1920, Height = 1080 };
            Assert.IsFalse(pos1 != pos2);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquals_BoxedIdenticalValues_ReturnsTrue()
        {
            var pos1 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            object pos2 = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            Assert.IsTrue(pos1.Equals(pos2));
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquals_NullComparison_ReturnsFalse()
        {
            var pos = new PositionWrapper { X = 0, Y = 0, Width = 100, Height = 100 };
            Assert.IsFalse(pos.Equals(null));
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void PositionEquals_DifferentObjectType_ReturnsFalse()
        {
            var pos = new PositionWrapper { X = 0, Y = 0, Width = 100, Height = 100 };
            Assert.IsFalse(pos.Equals("not a position"));
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void WindowPosition_LeftOfPrimaryMonitor_StoresNegativeCoordinates()
        {
            var pos = new PositionWrapper { X = -1920, Y = -200, Width = 1920, Height = 1080 };
            Assert.AreEqual(-1920, pos.X);
            Assert.AreEqual(-200, pos.Y);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void WindowPosition_AllZeroValues_IsValidState()
        {
            var pos = new PositionWrapper { X = 0, Y = 0, Width = 0, Height = 0 };
            Assert.AreEqual(0, pos.X);
            Assert.AreEqual(0, pos.Y);
            Assert.AreEqual(0, pos.Width);
            Assert.AreEqual(0, pos.Height);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void WindowPosition_FourthMonitor4K_StoresLargeCoordinates()
        {
            var pos = new PositionWrapper { X = 11520, Y = 0, Width = 3840, Height = 2160 };
            Assert.AreEqual(11520, pos.X);
            Assert.AreEqual(3840, pos.Width);
            Assert.AreEqual(2160, pos.Height);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void WindowPosition_DefaultStruct_AllFieldsAreZero()
        {
            PositionWrapper pos = default;
            Assert.AreEqual(0, pos.X);
            Assert.AreEqual(0, pos.Y);
            Assert.AreEqual(0, pos.Width);
            Assert.AreEqual(0, pos.Height);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void WindowPosition_TwoDefaultStructs_AreConsideredEqual()
        {
            PositionWrapper pos1 = default;
            PositionWrapper pos2 = default;
            Assert.IsTrue(pos1 == pos2);
            Assert.IsTrue(pos1.Equals(pos2));
        }
    }
}
