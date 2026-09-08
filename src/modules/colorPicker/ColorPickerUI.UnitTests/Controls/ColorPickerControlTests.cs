// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Controls
{
    [TestClass]
    public class ColorPickerControlTests
    {
        [DataTestMethod]
        [DataRow("0", 0, true)]
        [DataRow("128", 128, true)]
        [DataRow("255", 255, true)]
        [DataRow("256", 42, false)]
        [DataRow("999", 42, false)]
        [DataRow("-1", 42, false)]
        [DataRow("12.5", 42, false)]
        [DataRow("", 42, true)]
        [DataRow("not-a-number", 42, false)]
        public void Rgb_value_must_be_a_byte_before_conversion(string text, int expected, bool canBeEntered)
        {
            byte result = ColorPickerControl.GetValidatedRgbValue(text, previousValue: 42);

            Assert.AreEqual((byte)expected, result);
            Assert.AreEqual(canBeEntered, ColorPickerControl.IsRgbTextValid(text));
        }

        [DataTestMethod]
        [DataRow("abc", true)]
        [DataRow("#abc", true)]
        [DataRow("aabbcc", true)]
        [DataRow("#aabbcc", true)]
        [DataRow("", false)]
        [DataRow("#", false)]
        [DataRow("ab", false)]
        [DataRow("abcd", false)]
        [DataRow("zzzzzz", false)]
        [DataRow("#1234567", false)]
        public void Hex_value_must_be_complete_before_commit(string text, bool isValid)
        {
            Assert.AreEqual(isValid, ColorPickerControl.IsHexTextValid(text));
        }
    }
}
