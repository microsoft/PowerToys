// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;
using System.ComponentModel;
using ImageResizer.Properties;
using ImageResizer.Test;
using Xunit;

namespace ImageResizer.Models
{
    public class ResizeSizeTests
    {
        [Fact]
        public void NameWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Name = "Test");

            Assert.Equal("Test", size.Name);
            Assert.Equal(nameof(ResizeSize.Name), e.Arguments.PropertyName);
        }

        [Fact]
        public void NameReplacesTokens()
        {
            var args = new List<(string, string)>
            {
                ("$small$", Resources.Small),
                ("$medium$", Resources.Medium),
                ("$large$", Resources.Large),
                ("$phone$", Resources.Phone),
            };
            foreach (var (name, expected) in args)
            {
                var size = new ResizeSize
                {
                    Name = name,
                };

                Assert.Equal(expected, size.Name);
            }
        }

        [Fact]
        public void FitWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Fit = ResizeFit.Stretch);

            Assert.Equal(ResizeFit.Stretch, size.Fit);
            Assert.Equal(nameof(ResizeSize.Fit), e.Arguments.PropertyName);
        }

        [Fact]
        public void WidthWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Width = 42);

            Assert.Equal(42, size.Width);
            Assert.Equal(nameof(ResizeSize.Width), e.Arguments.PropertyName);
        }

        [Fact]
        public void HeightWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Height = 42);

            Assert.Equal(42, size.Height);
            Assert.Equal(nameof(ResizeSize.Height), e.Arguments.PropertyName);
        }

        [Fact]
        public void HasAutoReturnsTrueWhenWidthUnset()
        {
            var size = new ResizeSize
            {
                Width = 0,
                Height = 42,
            };

            Assert.True(size.HasAuto);
        }

        [Fact]
        public void HasAutoReturnsTrueWhenHeightUnset()
        {
            var size = new ResizeSize
            {
                Width = 42,
                Height = 0,
            };

            Assert.True(size.HasAuto);
        }

        [Fact]
        public void HasAutoReturnsFalseWhenWidthAndHeightSet()
        {
            var size = new ResizeSize
            {
                Width = 42,
                Height = 42,
            };

            Assert.False(size.HasAuto);
        }

        [Fact]
        public void UnitWorks()
        {
            var size = new ResizeSize();

            var e = AssertEx.Raises<PropertyChangedEventArgs>(
                h => size.PropertyChanged += h,
                h => size.PropertyChanged -= h,
                () => size.Unit = ResizeUnit.Inch);

            Assert.Equal(ResizeUnit.Inch, size.Unit);
            Assert.Equal(nameof(ResizeSize.Unit), e.Arguments.PropertyName);
        }

        [Fact]
        public void GetPixelWidthWorks()
        {
            var size = new ResizeSize
            {
                Width = 1,
                Unit = ResizeUnit.Inch,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(96, result);
        }

        [Fact]
        public void GetPixelHeightWorks()
        {
            var size = new ResizeSize
            {
                Height = 1,
                Unit = ResizeUnit.Inch,
            };

            var result = size.GetPixelHeight(100, 96);

            Assert.Equal(96, result);
        }

        [Theory]
        [InlineData(ResizeFit.Fit)]
        [InlineData(ResizeFit.Fill)]
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

            Assert.Equal(100, result);
        }

        [Fact]
        public void ConvertToPixelsWorksWhenAutoAndFit()
        {
            var size = new ResizeSize
            {
                Width = 0,
                Fit = ResizeFit.Fit,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(double.PositiveInfinity, result);
        }

        [Fact]
        public void ConvertToPixelsWorksWhenAutoAndNotFit()
        {
            var size = new ResizeSize
            {
                Width = 0,
                Fit = ResizeFit.Fill,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(100, result);
        }

        [Fact]
        public void ConvertToPixelsWorksWhenInches()
        {
            var size = new ResizeSize
            {
                Width = 0.5,
                Unit = ResizeUnit.Inch,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(48, result);
        }

        [Fact]
        public void ConvertToPixelsWorksWhenCentimeters()
        {
            var size = new ResizeSize
            {
                Width = 1,
                Unit = ResizeUnit.Centimeter,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(38, result, 0);
        }

        [Fact]
        public void ConvertToPixelsWorksWhenPercent()
        {
            var size = new ResizeSize
            {
                Width = 50,
                Unit = ResizeUnit.Percent,
            };

            var result = size.GetPixelWidth(200, 96);

            Assert.Equal(100, result);
        }

        [Fact]
        public void ConvertToPixelsWorksWhenPixels()
        {
            var size = new ResizeSize
            {
                Width = 50,
                Unit = ResizeUnit.Pixel,
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(50, result);
        }

        [Theory]
        [InlineData(ResizeFit.Fill, ResizeUnit.Centimeter)]
        [InlineData(ResizeFit.Fill, ResizeUnit.Inch)]
        [InlineData(ResizeFit.Fill, ResizeUnit.Pixel)]
        [InlineData(ResizeFit.Fit, ResizeUnit.Centimeter)]
        [InlineData(ResizeFit.Fit, ResizeUnit.Inch)]
        [InlineData(ResizeFit.Fit, ResizeUnit.Pixel)]
        [InlineData(ResizeFit.Stretch, ResizeUnit.Centimeter)]
        [InlineData(ResizeFit.Stretch, ResizeUnit.Inch)]
        [InlineData(ResizeFit.Stretch, ResizeUnit.Percent)]
        [InlineData(ResizeFit.Stretch, ResizeUnit.Pixel)]
        public void HeightVisible(ResizeFit fit, ResizeUnit unit)
        {
            var size = new ResizeSize
            {
                Fit = fit,
                Unit = unit,
            };

            Assert.True(size.ShowHeight);
        }

        [Theory]
        [InlineData(ResizeFit.Fill, ResizeUnit.Percent)]
        [InlineData(ResizeFit.Fit, ResizeUnit.Percent)]
        public void HeightNotVisible(ResizeFit fit, ResizeUnit unit)
        {
            var size = new ResizeSize
            {
                Fit = fit,
                Unit = unit,
            };

            Assert.False(size.ShowHeight);
        }
    }
}
