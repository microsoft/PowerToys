// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Converters;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for BooleanToInvertedVisibilityConverter.
    /// When Loading=true  → spinner Visible, glyph Collapsed
    /// When Loading=false → spinner Collapsed, glyph Visible
    /// </summary>
    [TestClass]
    public class StatusIndicatorVisibilityTests
    {
        private readonly BooleanToInvertedVisibilityConverter _converter = new();

        [TestMethod]
        [TestCategory("Converter")]
        public void Converter_WhenLoadingTrue_HidesStatusGlyph()
        {
            var result = _converter.Convert(true, typeof(Visibility), null, "en-US");
            Assert.AreEqual(Visibility.Collapsed, result);
        }

        [TestMethod]
        [TestCategory("Converter")]
        public void Converter_WhenLoadingFalse_ShowsStatusGlyph()
        {
            var result = _converter.Convert(false, typeof(Visibility), null, "en-US");
            Assert.AreEqual(Visibility.Visible, result);
        }

        [TestMethod]
        [TestCategory("Converter")]
        [ExpectedException(typeof(NotImplementedException))]
        public void Converter_ReverseConversion_IsNotSupported()
        {
            _converter.ConvertBack(Visibility.Visible, typeof(bool), null, "en-US");
        }

        [TestMethod]
        [TestCategory("Converter")]
        public void Converter_NullConverterParameter_StillFunctionsCorrectly()
        {
            var result = _converter.Convert(true, typeof(Visibility), null, null);
            Assert.AreEqual(Visibility.Collapsed, result);
        }

        [TestMethod]
        [TestCategory("Converter")]
        public void Converter_MultipleCultures_BehaviorIsCultureInvariant()
        {
            var result1 = _converter.Convert(true, typeof(Visibility), null, "en-US");
            var result2 = _converter.Convert(true, typeof(Visibility), null, "ja-JP");
            var result3 = _converter.Convert(true, typeof(Visibility), null, "de-DE");

            Assert.AreEqual(Visibility.Collapsed, result1);
            Assert.AreEqual(Visibility.Collapsed, result2);
            Assert.AreEqual(Visibility.Collapsed, result3);
        }
    }
}
