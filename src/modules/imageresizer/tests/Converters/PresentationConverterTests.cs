// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using ImageResizer.Helpers;
using ImageResizer.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Converters
{
    [TestClass]
    [DoNotParallelize]
    public sealed class PresentationConverterTests
    {
        private CultureInfo _originalCulture;
        private Func<string, string> _originalGetString;

        [TestInitialize]
        public void Initialize()
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalGetString = ResourceLoaderInstance.GetString;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResourceLoaderInstance.GetString = _originalGetString;
            CultureInfo.CurrentCulture = _originalCulture;
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(double.NaN)]
        public void AutoDouble_ConvertsAutoDimensionsToResourceLabel(double dimension)
        {
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return "MiXeD automatic label";
            };

            var result = new AutoDoubleConverter().Convert(dimension, typeof(string), "Auto", "en-US");

            Assert.AreEqual("Auto", requestedKey);
            Assert.AreEqual("MiXeD automatic label", result);
        }

        [TestMethod]
        public void AutoDouble_FormatsFiniteDimensionUsingCurrentCulture()
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var result = new AutoDoubleConverter().Convert(1.5, typeof(string), "Auto", "en-US");

            Assert.AreEqual("1,5", result);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fill, 0)]
        [DataRow(ResizeFit.Fit, 1)]
        [DataRow(ResizeFit.Stretch, 2)]
        [DataRow(ResizeUnit.Centimeter, 0)]
        [DataRow(ResizeUnit.Inch, 1)]
        [DataRow(ResizeUnit.Percent, 2)]
        [DataRow(ResizeUnit.Pixel, 3)]
        public void EnumToInt_RoundTripsFitAndUnitSelections(object selection, int expectedIndex)
        {
            var converter = new EnumToIntConverter();

            var index = converter.Convert(selection, typeof(int), null, "en-US");
            var restoredSelection = converter.ConvertBack(index, selection.GetType(), null, "en-US");

            Assert.IsInstanceOfType<int>(index);
            Assert.AreEqual(expectedIndex, index);
            Assert.AreEqual(selection.GetType(), restoredSelection?.GetType());
            Assert.AreEqual(selection, restoredSelection);
        }

        [TestMethod]
        public void EnumToInt_PreservesUnselectedIndex()
        {
            var result = new EnumToIntConverter().ConvertBack(-1, typeof(ResizeUnit), null, "en-US");

            Assert.IsInstanceOfType<ResizeUnit>(result);
            Assert.AreEqual(-1, (int)(ResizeUnit)result);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fit, "ResizeFit_Fit")]
        [DataRow(ResizeUnit.Centimeter, "ResizeUnit_Centimeter")]
        public void EnumValue_ReturnsTranslatedFitAndUnitLabels(object selection, string expectedKey)
        {
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return "MiXeD translated label";
            };

            var result = new EnumValueConverter().Convert(selection, typeof(string), null, "en-US");

            Assert.AreEqual(expectedKey, requestedKey);
            Assert.AreEqual("MiXeD translated label", result);
        }

        [TestMethod]
        public void EnumValue_LowercasesUsingExplicitLanguage()
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return "INDIGO";
            };

            var result = new EnumValueConverter().Convert(ResizeUnit.Inch, typeof(string), "ToLower", "tr-TR");

            Assert.AreEqual("ResizeUnit_Inch", requestedKey);
            Assert.AreEqual("\u0131nd\u0131go", result);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void EnumValue_LowercasesUsingCurrentCultureWithoutLanguage(string language)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return "INDIGO";
            };

            var result = new EnumValueConverter().Convert(ResizeUnit.Inch, typeof(string), "ToLower", language);

            Assert.AreEqual("ResizeUnit_Inch", requestedKey);
            Assert.AreEqual("\u0131nd\u0131go", result);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void EnumValue_PreservesMissingTranslationWhenLowercasing(string translation)
        {
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return translation;
            };

            var result = new EnumValueConverter().Convert(ResizeUnit.Inch, typeof(string), "ToLower", "tr-TR");

            Assert.AreEqual("ResizeUnit_Inch", requestedKey);
            Assert.AreEqual(translation, result);
        }
    }
}
