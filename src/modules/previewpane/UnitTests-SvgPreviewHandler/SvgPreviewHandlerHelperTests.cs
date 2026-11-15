// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

using Common.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SvgPreviewHandlerUnitTests
{
    [STATestClass]
    public class SvgPreviewHandlerHelperTests
    {
        [DataTestMethod]
        [DataRow("<svg?>")]
        [DataRow("<svg?>?>")]
        [DataRow("<svg")]
        [Timeout(5000)]
        public void AddStyleSvgWithoutClosingTagShouldReturnOriginalInput(string svgData)
        {
            Assert.AreEqual(svgData, SvgPreviewHandlerHelper.AddStyleSVG(svgData));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" \r\n")]
        [DataRow("\uFEFF<?xml version=\"1.0\"?>\r\n<!-- illustration -->\r\n")]
        [Timeout(5000)]
        public void AddStyleSvgShouldPreserveSupportedPrefixes(string prefix)
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 50\" width=\"100\" height=\"50\"><path d=\"M0 0 L100 50\"/><rect x=\"10\" y=\"10\" width=\"40\" height=\"20\" fill=\"blue\"/></svg>";
            var actual = SvgPreviewHandlerHelper.AddStyleSVG(prefix + svg);
            var expectedPrefix = prefix.Replace("<?xml version=\"1.0\"?>", string.Empty);

            StringAssert.StartsWith(actual, expectedPrefix + "<svg ");
            StringAssert.Contains(actual, "xmlns=\"http://www.w3.org/2000/svg\"");
            StringAssert.Contains(actual, "viewBox=\"0 0 100 50\"");
            StringAssert.Contains(actual, "style=\"");
            StringAssert.Contains(actual, "max-width: 100px");
            StringAssert.Contains(actual, "max-height: 50px");
            StringAssert.EndsWith(actual, "<path d=\"M0 0 L100 50\"/><rect x=\"10\" y=\"10\" width=\"40\" height=\"20\" fill=\"blue\"/></svg>");
            Assert.IsFalse(actual.Contains("width=\"100\""));
            Assert.IsFalse(actual.Contains("height=\"50\""));
            Assert.IsFalse(actual.Contains("<?xml"));
        }

        [TestMethod]
        [Timeout(5000)]
        public void AddStyleSvgShouldAdvancePastProcessingInstructionTerminatorInsideAttribute()
        {
            const string svg = "<svg data-note=\"?>\" viewBox=\"0 0 100 50\"><path d=\"M0 0 L100 50\"/></svg>";

            var actual = SvgPreviewHandlerHelper.AddStyleSVG(svg);

            StringAssert.StartsWith(actual, "<svg data-note=\"?>\" viewBox=\"0 0 100 50\" style=\"");
            StringAssert.Contains(actual, "position: absolute; top: 50%; left: 50%;");
            StringAssert.EndsWith(actual, "<path d=\"M0 0 L100 50\"/></svg>");
        }

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
    }
}
