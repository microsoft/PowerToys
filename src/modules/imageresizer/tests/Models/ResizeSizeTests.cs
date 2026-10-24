#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.ComponentModel;
using System.Linq;

using ImageResizer.Helpers;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeSizeTests
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            ResourceLoaderInstance.GetString = key => key;
        }

        [TestMethod]
        public void NameWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Name = "Test");

            Assert.AreEqual("Test", size.Name);
            Assert.AreEqual(nameof(ResizeSize.Name), e.Arguments.PropertyName);
        }

        [TestMethod]
        public void NameReplacesTokens()
        {
            var size = new ResizeSize();

            size.Name = "$small$";

            Assert.AreEqual("Small", size.Name);
        }

        [TestMethod]
        public void FitWorks()
        {
            var size = new ResizeSize();

            var events = AssertEx.RaisesAll<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Fit = ResizeFit.Stretch);

            Assert.AreEqual(ResizeFit.Stretch, size.Fit);
            Assert.IsTrue(
                events.Any(e => e.Arguments.PropertyName == nameof(ResizeSize.Fit)),
                "Expected PropertyChanged for Fit");
        }

        [TestMethod]
        public void WidthWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Width = 42);

            Assert.AreEqual(42, size.Width);
            Assert.AreEqual(nameof(ResizeSize.Width), e.Arguments.PropertyName);
        }

        [TestMethod]
        public void HeightWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Height = 42);

            Assert.AreEqual(42, size.Height);
            Assert.AreEqual(nameof(ResizeSize.Height), e.Arguments.PropertyName);
        }

        [DataTestMethod]
        [DataRow(0.0)]
        [DataRow(double.NaN)]
        public void HasAutoReturnsTrueWhenWidthUnset(double width)
        {
            var size = new ResizeSize
            {
                Width = width,
                Height = 42,
            };

            Assert.IsTrue(size.HasAuto);
        }

        [DataTestMethod]
        [DataRow(0.0)]
        [DataRow(double.NaN)]
        public void HasAutoReturnsTrueWhenHeightUnset(double height)
        {
            var size = new ResizeSize
            {
                Width = 42,
                Height = height,
            };

            Assert.IsTrue(size.HasAuto);
        }

        [TestMethod]
        public void HasAutoReturnsFalseWhenWidthAndHeightSet()
        {
            var size = new ResizeSize
            {
                Width = 42,
                Height = 42,
            };

            Assert.IsFalse(size.HasAuto);
        }

        [TestMethod]
        public void UnitWorks()
        {
            var size = new ResizeSize();

            var events = AssertEx.RaisesAll<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Unit = ResizeUnit.Inch);

            Assert.AreEqual(ResizeUnit.Inch, size.Unit);
            Assert.IsTrue(
                events.Any(e => e.Arguments.PropertyName == nameof(ResizeSize.Unit)),
                "Expected PropertyChanged for Unit");
        }

        [TestMethod]
        public void GetPixelWidthWorks()
        {
            var size = new ResizeSize
            {
                Width = 1,
                Unit = ResizeUnit.Inch,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.AreEqual(96, result);
        }

        [TestMethod]
        public void GetPixelHeightWorks()
        {
            var size = new ResizeSize
            {
                Height = 1,
                Unit = ResizeUnit.Inch,
            };

            var result = size.GetPixelHeight(100, 96);

            Assert.AreEqual(96, result);
        }

        [DataTestMethod]
        [DataRow(ResizeFit.Fit)]
        [DataRow(ResizeFit.Fill)]
        public void GetPixelHeightUsesWidthWhenScaleByPercent(ResizeFit fit)
        {
            var size = new ResizeSize
            {
                Fit = fit,
                Width = 100,
                Height = 50,
                Unit = ResizeUnit.Percent,
            };

            var result = size.GetPixelHeight(100, 96);

            Assert.AreEqual(100, result);
        }

        [DataTestMethod]
        [DataRow(0.0)]
        [DataRow(double.NaN)]
        public void ConvertToPixelsWorksWhenAutoAndFit(double value)
        {
            var size = new ResizeSize
            {
                Width = value,
                Height = value,
                Fit = ResizeFit.Fit,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.AreEqual(double.PositiveInfinity, result);
            Assert.AreEqual(double.PositiveInfinity, size.GetPixelHeight(200, 96));
        }

        [DataTestMethod]
        [DataRow(ResizeFit.Fill, 0.0)]
        [DataRow(ResizeFit.Fill, double.NaN)]
        [DataRow(ResizeFit.Stretch, 0.0)]
        [DataRow(ResizeFit.Stretch, double.NaN)]
        public void ConvertToPixelsWorksWhenAutoAndNotFit(ResizeFit fit, double value)
        {
            var size = new ResizeSize
            {
                Width = value,
                Height = value,
                Fit = fit,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.AreEqual(100, result);
            Assert.AreEqual(200, size.GetPixelHeight(200, 96));
        }

        [TestMethod]
        public void ConvertToPixelsWorksWhenInches()
        {
            var size = new ResizeSize
            {
                Width = 0.5,
                Unit = ResizeUnit.Inch,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.AreEqual(48, result);
        }

        [TestMethod]
        public void ConvertToPixelsWorksWhenCentimeters()
        {
            var size = new ResizeSize
            {
                Width = 1,
                Unit = ResizeUnit.Centimeter,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.AreEqual(38, Math.Ceiling(result));
        }

        [TestMethod]
        public void ConvertToPixelsWorksWhenPercent()
        {
            var size = new ResizeSize
            {
                Width = 50,
                Unit = ResizeUnit.Percent,
            };

            var result = size.GetPixelWidth(200, 96);

            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void ConvertToPixelsWorksWhenPixels()
        {
            var size = new ResizeSize
            {
                Width = 50,
                Unit = ResizeUnit.Pixel,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.AreEqual(50, result);
        }

        [DataTestMethod]
        [DataRow(ResizeUnit.Centimeter, 10.0, 5.0, 96.0, 377.9527559055, 188.9763779528)]
        [DataRow(ResizeUnit.Centimeter, 10.0, 5.0, 120.0, 472.4409448819, 236.2204724409)]
        [DataRow(ResizeUnit.Inch, 4.0, 3.0, 96.0, 384.0, 288.0)]
        [DataRow(ResizeUnit.Inch, 4.0, 3.0, 120.0, 480.0, 360.0)]
        [DataRow(ResizeUnit.Pixel, 500.0, 300.0, 96.0, 500.0, 300.0)]
        [DataRow(ResizeUnit.Pixel, 500.0, 300.0, 120.0, 500.0, 300.0)]
        [DataRow(ResizeUnit.Percent, 50.0, 25.0, 120.0, 600.0, 200.0)]
        public void ConvertToPixelsPreservesPrecisionAndIndependentDimensions(
            ResizeUnit unit,
            double width,
            double height,
            double dpi,
            double expectedWidth,
            double expectedHeight)
        {
            var size = new ResizeSize
            {
                Fit = ResizeFit.Stretch,
                Unit = unit,
                Width = width,
                Height = height,
            };

            Assert.AreEqual(expectedWidth, size.GetPixelWidth(1200, dpi), 1e-9);
            Assert.AreEqual(expectedHeight, size.GetPixelHeight(800, dpi), 1e-9);
        }

        [DataTestMethod]
        [DataRow(96.0)]
        [DataRow(120.0)]
        public void ConvertToPixelsUsesWidthForPercentFitAtDeclaredDpi(double dpi)
        {
            var size = new ResizeSize
            {
                Name = "Test",
                Fit = ResizeFit.Fit,
                Unit = ResizeUnit.Percent,
                Width = 50,
                Height = 200,
            };

            Assert.AreEqual(600.0, size.GetPixelWidth(1200, dpi), 1e-9);
            Assert.AreEqual(400.0, size.GetPixelHeight(800, dpi), 1e-9);
        }

        [DataTestMethod]
        [DataRow(ResizeFit.Fill, ResizeUnit.Centimeter)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Inch)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Pixel)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Centimeter)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Inch)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Pixel)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Centimeter)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Inch)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Percent)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Pixel)]
        public void HeightVisible(ResizeFit fit, ResizeUnit unit)
        {
            var size = new ResizeSize
            {
                Fit = fit,
                Unit = unit,
            };

            Assert.IsTrue(size.ShowHeight);
        }

        [DataTestMethod]
        [DataRow(ResizeFit.Fill, ResizeUnit.Percent)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Percent)]
        public void HeightNotVisible(ResizeFit fit, ResizeUnit unit)
        {
            var size = new ResizeSize
            {
                Fit = fit,
                Unit = unit,
            };

            Assert.IsFalse(size.ShowHeight);
        }

        [TestMethod]
        public void ShowHeightNotifiesWhenFitOrUnitChanges()
        {
            var size = new ResizeSize
            {
                Fit = ResizeFit.Fit,
                Unit = ResizeUnit.Percent,
            };

            Assert.IsFalse(size.ShowHeight);

            var transitions = new (Action Change, bool ExpectedShowHeight)[]
            {
                (() => size.Fit = ResizeFit.Stretch, true),
                (() => size.Unit = ResizeUnit.Pixel, true),
                (() => size.Fit = ResizeFit.Fit, true),
                (() => size.Unit = ResizeUnit.Percent, false),
            };

            foreach (var transition in transitions)
            {
                var events = AssertEx.RaisesAll<PropertyChangedEventArgs>(
                    h => size.PropertyChanged += h,
                    h => size.PropertyChanged -= h,
                    transition.Change);

                Assert.AreEqual(transition.ExpectedShowHeight, size.ShowHeight);
                Assert.IsTrue(
                    events.Any(e => e.Arguments.PropertyName == nameof(ResizeSize.ShowHeight)),
                    "Expected PropertyChanged for ShowHeight");
            }
        }
    }
}
