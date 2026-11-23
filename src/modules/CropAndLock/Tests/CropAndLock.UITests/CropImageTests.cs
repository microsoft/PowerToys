// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CropAndLock.UITests
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("CropAndLock")]
    public sealed class CropImageTests
    {
        [TestMethod]
        public void ComparisonReportsBothDimensions()
        {
            using var expected = CreateBitmap(32, 32, Color.Blue);
            using var actual = CreateBitmap(33, 32, Color.Blue);

            var comparison = CropImage.Compare(new CropImage(expected), new CropImage(actual));

            Assert.IsFalse(comparison.Matches);
            Assert.AreEqual(expected.Size, comparison.ExpectedSize);
            Assert.AreEqual(actual.Size, comparison.ActualSize);
        }

        [TestMethod]
        public void IdenticalContentMatchesAcrossRepeatedComparisons()
        {
            using var bitmap = CreateBitmap(32, 32, Color.Blue);
            var expected = new CropImage(bitmap);
            var actual = new CropImage(bitmap);

            var first = CropImage.Compare(expected, actual);
            var repeated = CropImage.Compare(expected, actual);

            Assert.IsTrue(first.Matches);
            Assert.AreEqual(first, repeated);
            Assert.AreEqual(1.0, first.ContentPixels);
        }

        [TestMethod]
        public void BlankImagesDoNotPassOnBackgroundAgreement()
        {
            using var bitmap = CreateBitmap(32, 32, Color.White);

            var comparison = CropImage.Compare(new CropImage(bitmap), new CropImage(bitmap));

            Assert.AreEqual(1.0, comparison.AllPixels);
            Assert.AreEqual(0, comparison.ContentCount);
            Assert.IsFalse(comparison.Matches);
        }

        [TestMethod]
        public void ChangedForegroundDoesNotMatch()
        {
            using var expected = CreateBitmap(32, 32, Color.Blue);
            using var actual = CreateBitmap(32, 32, Color.Red);

            var comparison = CropImage.Compare(new CropImage(expected), new CropImage(actual));

            Assert.IsTrue(comparison.ContentCount >= 100);
            Assert.IsFalse(comparison.Matches);
        }

        private static Bitmap CreateBitmap(int width, int height, Color foreground)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            using var brush = new SolidBrush(foreground);
            graphics.Clear(Color.White);
            graphics.FillRectangle(brush, 8, 8, 16, 16);
            return bitmap;
        }
    }
}
