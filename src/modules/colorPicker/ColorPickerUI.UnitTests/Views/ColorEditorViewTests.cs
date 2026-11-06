// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ColorPicker.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace ColorPicker.UnitTests.Views
{
    [TestClass]
    public class ColorEditorViewTests
    {
        private static readonly Color Red = new Color { A = 255, R = 255, G = 0, B = 0 };
        private static readonly Color Blue = new Color { A = 255, R = 0, G = 0, B = 255 };

        [TestMethod]
        public void GetDistinctSelectedColors_DuplicateColors_ReturnsEachColorOnce()
        {
            var result = ColorEditorView.GetDistinctSelectedColors(new List<object> { Red, Red, Blue });

            CollectionAssert.AreEqual(new List<Color> { Red, Blue }, result);
        }

        [TestMethod]
        public void GetDistinctSelectedColors_NonColorItems_AreIgnored()
        {
            var result = ColorEditorView.GetDistinctSelectedColors(new List<object> { "red", Red, 42 });

            CollectionAssert.AreEqual(new List<Color> { Red }, result);
        }

        [TestMethod]
        public void GetDistinctSelectedColors_EmptyList_ReturnsEmpty()
        {
            var result = ColorEditorView.GetDistinctSelectedColors(new List<object>());

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        [DataRow(false, false, true)]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, true, false)]
        public void ShouldStartPicking_FocusAndFlyoutState_ReturnsExpected(bool isTextInputFocused, bool isAdjustFlyoutOpen, bool expected)
        {
            Assert.AreEqual(expected, ColorEditorView.ShouldStartPicking(isTextInputFocused, isAdjustFlyoutOpen));
        }
    }
}
