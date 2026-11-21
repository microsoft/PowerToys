// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

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

        [DataTestMethod]
        [DataRow("stroke-width", "1.5")]
        [DataRow("data-height", "2.5")]
        [DataRow("font-style", "italic")]
        public void AddStyleSVGShouldNotMatchAttributeNamesThatEndWithWidthHeightOrStyle(string attributeName, string attributeValue)
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" fill=\"none\" viewBox=\"0 0 24 24\" {attributeName}=\"{attributeValue}\" stroke=\"currentColor\">");
            svgBuilder.AppendLine("\t<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M4.5 12.75l6 6 9-13.5\" />");
            svgBuilder.AppendLine("</svg>");

            // Act
            string result = SvgPreviewHandlerHelper.AddStyleSVG(svgBuilder.ToString());

            // Assert
            XElement root = XElement.Parse(result);
            Assert.AreEqual(attributeValue, root.Attribute(attributeName)?.Value);
            Assert.IsFalse((root.Attribute("style")?.Value ?? string.Empty).Contains(attributeValue));
        }

        [TestMethod]
        public void AddStyleSVGShouldUseRootWidthAndHeightWhenStrokeWidthComesFirst()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" stroke-width=\"2\" width=\"24\" height=\"32\" viewBox=\"0 0 24 32\">");
            svgBuilder.AppendLine("\t<circle cx=\"12\" cy=\"16\" r=\"10\"/>");
            svgBuilder.AppendLine("</svg>");

            // Act
            string result = SvgPreviewHandlerHelper.AddStyleSVG(svgBuilder.ToString());

            // Assert
            XElement root = XElement.Parse(result);
            Assert.AreEqual("2", root.Attribute("stroke-width")?.Value);
            Assert.IsNull(root.Attribute("width"));
            Assert.IsNull(root.Attribute("height"));
            Assert.IsTrue((root.Attribute("style")?.Value ?? string.Empty).StartsWith("max-width: 24px ; max-height: 32px ;", StringComparison.Ordinal));
        }

        [TestMethod]
        public void AddStyleSVGShouldOnlyModifyRootTagWhenPrecededByPrologAndComment()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
            svgBuilder.AppendLine("<!-- Generator: Adobe Illustrator 27.5.0, SVG Export Plug-In . SVG Version: 6.00 Build 0)  -->");
            svgBuilder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">");
            svgBuilder.AppendLine("\t<rect x=\"10\" y=\"10\" width=\"80\" height=\"60\" style=\"fill:red\"/>");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"20\" fill=\"blue\"/>");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"10\" fill=\"green\"/>");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"5\" fill=\"white\"/>");
            svgBuilder.AppendLine("</svg>");

            // Act
            string result = SvgPreviewHandlerHelper.AddStyleSVG(svgBuilder.ToString());

            // Assert
            XElement root = XElement.Parse(result);
            XElement? rect = root.Element(XName.Get("rect", "http://www.w3.org/2000/svg"));
            Assert.AreEqual("80", rect?.Attribute("width")?.Value);
            Assert.AreEqual("60", rect?.Attribute("height")?.Value);
            Assert.AreEqual("fill:red", rect?.Attribute("style")?.Value);
            Assert.AreEqual("0 0 100 100", root.Attribute("viewBox")?.Value);
            Assert.IsNotNull(root.Attribute("style"));
        }

        [TestMethod]
        public void AddStyleSVGShouldNotThrowWhenRootTagIsPrecededByLongComment()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<!-- " + new string('x', 200) + " -->");
            svgBuilder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"></svg>");

            // Act
            string result = SvgPreviewHandlerHelper.AddStyleSVG(svgBuilder.ToString());

            // Assert
            XElement root = XElement.Parse(result);
            Assert.IsNull(root.Attribute("width"));
            Assert.IsNull(root.Attribute("height"));
            Assert.AreEqual("0 0 16 16", root.Attribute("viewBox")?.Value);
            Assert.IsTrue((root.Attribute("style")?.Value ?? string.Empty).StartsWith("max-width: 16px ; max-height: 16px ;", StringComparison.Ordinal));
        }

        [TestMethod]
        public void AddStyleSVGShouldMoveRootWidthHeightAndStyleIntoNewStyle()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg width=\"200\" height=\"100pt\" style=\"fill:blue\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<rect width=\"10\" height=\"20\"/>");
            svgBuilder.AppendLine("</svg>");

            // Act
            string result = SvgPreviewHandlerHelper.AddStyleSVG(svgBuilder.ToString());

            // Assert
            XElement root = XElement.Parse(result);
            XElement? rect = root.Element(XName.Get("rect", "http://www.w3.org/2000/svg"));
            string style = root.Attribute("style")?.Value ?? string.Empty;
            Assert.IsNull(root.Attribute("width"));
            Assert.IsNull(root.Attribute("height"));
            Assert.IsTrue(style.StartsWith("max-width: 200px ; max-height: 100pt ;", StringComparison.Ordinal));
            Assert.IsTrue(style.EndsWith("fill:blue", StringComparison.Ordinal));
            Assert.AreEqual("0 0 200 100", root.Attribute("viewBox")?.Value);
            Assert.AreEqual("10", rect?.Attribute("width")?.Value);
            Assert.AreEqual("20", rect?.Attribute("height")?.Value);
        }
    }
}
