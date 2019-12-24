using System.Collections.Generic;
using System.ComponentModel;
using ImageResizer.Properties;
using ImageResizer.Test;
using Xunit;
using Xunit.Extensions;

namespace ImageResizer.Models
{
    public class ResizeSizeTests
    {
        [Fact]
        public void Name_works()
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
        public void Name_replaces_tokens()
        {
            var args = new List<(string, string)>
            {
                ("$small$", Resources.Small),
                ("$medium$", Resources.Medium),
                ("$large$", Resources.Large),
                ("$phone$", Resources.Phone)
            };
            foreach (var (name, expected) in args)
            {
                var size = new ResizeSize();

                size.Name = name;

                Assert.Equal(expected, size.Name);
            }
        }

        [Fact]
        public void Fit_works()
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
        public void Width_works()
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
        public void Height_works()
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
        public void HasAuto_returns_true_when_Width_unset()
        {
            var size = new ResizeSize
            {
                Width = 0,
                Height = 42
            };

            Assert.True(size.HasAuto);
        }

        [Fact]
        public void HasAuto_returns_true_when_Height_unset()
        {
            var size = new ResizeSize
            {
                Width = 42,
                Height = 0
            };

            Assert.True(size.HasAuto);
        }

        [Fact]
        public void HasAuto_returns_false_when_Width_and_Height_set()
        {
            var size = new ResizeSize
            {
                Width = 42,
                Height = 42
            };

            Assert.False(size.HasAuto);
        }

        [Fact]
        public void Unit_works()
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
        public void GetPixelWidth_works()
        {
            var size = new ResizeSize
            {
                Width = 1,
                Unit = ResizeUnit.Inch
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(96, result);
        }

        [Fact]
        public void GetPixelHeight_works()
        {
            var size = new ResizeSize
            {
                Height = 1,
                Unit = ResizeUnit.Inch
            };

            var result = size.GetPixelHeight(100, 96);

            Assert.Equal(96, result);
        }

        [Theory]
        [InlineData(ResizeFit.Fit)]
        [InlineData(ResizeFit.Fill)]
        public void GetPixelHeight_uses_Width_when_scale_by_percent(ResizeFit fit)
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
        public void ConvertToPixels_works_when_auto_and_fit()
        {
            var size = new ResizeSize
            {
                Width = 0,
                Fit = ResizeFit.Fit
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(double.PositiveInfinity, result);
        }

        [Fact]
        public void ConvertToPixels_works_when_auto_and_not_fit()
        {
            var size = new ResizeSize
            {
                Width = 0,
                Fit = ResizeFit.Fill
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(100, result);
        }

        [Fact]
        public void ConvertToPixels_works_when_inches()
        {
            var size = new ResizeSize
            {
                Width = 0.5,
                Unit = ResizeUnit.Inch
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(48, result);
        }

        [Fact]
        public void ConvertToPixels_works_when_centimeters()
        {
            var size = new ResizeSize
            {
                Width = 1,
                Unit = ResizeUnit.Centimeter
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(38, result, 0);
        }

        [Fact]
        public void ConvertToPixels_works_when_percent()
        {
            var size = new ResizeSize
            {
                Width = 50,
                Unit = ResizeUnit.Percent
            };

            var result = size.GetPixelWidth(200, 96);

            Assert.Equal(100, result);
        }

        [Fact]
        public void ConvertToPixels_works_when_pixels()
        {
            var size = new ResizeSize
            {
                Width = 50,
                Unit = ResizeUnit.Pixel
            };

            var result = size.GetPixelWidth(100, 96);

            Assert.Equal(50, result);
        }
    }
}
