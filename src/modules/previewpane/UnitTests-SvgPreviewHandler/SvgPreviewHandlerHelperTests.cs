// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;

using Common.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SvgPreviewHandlerUnitTests
{
    [STATestClass]
    public class SvgPreviewHandlerHelperTests
    {
        [TestMethod]
        public void CheckBlockedElementsShouldReturnTrueIfABlockedElementIsPresent()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg width =\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");
            svgBuilder.AppendLine("\t<script>alert(\"hello\")</script>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnTrueIfBlockedElementsIsPresentInNestedLevel()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("\t\t<script>alert(\"valid-message\")</script>");
            svgBuilder.AppendLine("\t</circle>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnTrueIfMultipleBlockedElementsArePresent()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg width =\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");
            svgBuilder.AppendLine("\t<script>alert(\"valid-message\")</script>");
            svgBuilder.AppendLine("\t<image href=\"valid-url\" height=\"200\" width=\"200\"/>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnFalseIfNoBlockedElementsArePresent()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("\t</circle>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsFalse(foundFilteredElement);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow(null)]
        public void CheckBlockedElementsShouldReturnFalseIfSvgDataIsNullOrWhiteSpaces(string svgData)
        {
            // Arrange
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgData);

            // Assert
            Assert.IsFalse(foundFilteredElement);
        }

        [TestMethod]
        public void BuildCacheKeyShouldReturnSameValueForSameInputs()
        {
            // Arrange
            var firstKey = SvgPreviewCacheHelper.BuildCacheKey("v1", "svg-preview", "sample data");

            // Act
            var secondKey = SvgPreviewCacheHelper.BuildCacheKey("v1", "svg-preview", "sample data");

            // Assert
            Assert.AreEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void BuildCacheKeyShouldReturnDifferentValueForDifferentInputs()
        {
            // Arrange
            var firstKey = SvgPreviewCacheHelper.BuildCacheKey("v1", "svg-preview", "sample data");

            // Act
            var secondKey = SvgPreviewCacheHelper.BuildCacheKey("v1", "svg-preview", "different data");

            // Assert
            Assert.AreNotEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void BuildCacheKeyShouldNotCollideOnDelimiterAmbiguity()
        {
            // Arrange
            var firstKey = SvgPreviewCacheHelper.BuildCacheKey("a\nb", "");

            // Act
            var secondKey = SvgPreviewCacheHelper.BuildCacheKey("a", "b\n");

            // Assert
            Assert.AreNotEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void ManageCacheSizeShouldEvictOldestFiles()
        {
            // Arrange
            var cacheFolder = Path.Combine(Path.GetTempPath(), "SvgPreviewCacheTest");
            Directory.CreateDirectory(cacheFolder);

            try
            {
                // Create 5 dummy html files
                var files = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 5; i++)
                {
                    var path = Path.Combine(cacheFolder, $"test{i}.html");
                    File.WriteAllText(path, "test");
                    File.SetLastWriteTimeUtc(path, System.DateTime.UtcNow.AddMinutes(-i)); // test0 is newest, test4 is oldest
                    files.Add(path);
                }

                // Act - limit to 3
                SvgPreviewCacheHelper.ManageCacheSize(cacheFolder, 3);

                // Assert
                Assert.IsTrue(File.Exists(files[0])); // Newest
                Assert.IsTrue(File.Exists(files[1]));
                Assert.IsTrue(File.Exists(files[2]));
                Assert.IsFalse(File.Exists(files[3])); // Evicted
                Assert.IsFalse(File.Exists(files[4])); // Oldest evicted
            }
            finally
            {
                if (Directory.Exists(cacheFolder))
                {
                    Directory.Delete(cacheFolder, true);
                }
            }
        }
    }
}
