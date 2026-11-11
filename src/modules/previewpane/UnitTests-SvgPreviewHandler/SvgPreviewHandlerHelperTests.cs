// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

using Common.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SvgPreviewHandlerUnitTests
{
    [OSCondition(OperatingSystems.Windows)]
    [STATestClass]
    public class SvgPreviewHandlerHelperTests
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
        [TestMethod]
        [DataRow("<svg?>")]
        [DataRow("<svg?>?>")]
        [DataRow("<svg")]
        [Timeout(5000, CooperativeCancellation = false)]
        public void AddStyleSvgWithoutClosingTagShouldReturnOriginalInput(string svgData)
        {
            Assert.AreEqual(svgData, SvgPreviewHandlerHelper.AddStyleSVG(svgData));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
        [TestMethod]
        [DataRow("")]
        [DataRow(" \r\n")]
        [DataRow("\uFEFF<?xml version=\"1.0\"?>\r\n<!-- illustration -->\r\n")]
        [Timeout(5000, CooperativeCancellation = false)]
        public void AddStyleSvgShouldPreserveSupportedPrefixes(string prefix)
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 50\" width=\"100\" height=\"50\"><path d=\"M0 0 L100 50\"/><rect x=\"10\" y=\"10\" width=\"40\" height=\"20\" fill=\"blue\"/></svg>";
            var actual = SvgPreviewHandlerHelper.AddStyleSVG(prefix + svg);
            var expectedPrefix = prefix.Replace("<?xml version=\"1.0\"?>", string.Empty);

            Assert.StartsWith(expectedPrefix + "<svg ", actual);
            Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", actual);
            Assert.Contains("viewBox=\"0 0 100 50\"", actual);
            Assert.Contains("style=\"", actual);
            Assert.Contains("max-width: 100px", actual);
            Assert.Contains("max-height: 50px", actual);
            Assert.EndsWith("<path d=\"M0 0 L100 50\"/><rect x=\"10\" y=\"10\" width=\"40\" height=\"20\" fill=\"blue\"/></svg>", actual);
            Assert.DoesNotContain("width=\"100\"", actual);
            Assert.DoesNotContain("height=\"50\"", actual);
            Assert.DoesNotContain("<?xml", actual);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
        [TestMethod]
        [Timeout(5000, CooperativeCancellation = false)]
        public void AddStyleSvgShouldAdvancePastProcessingInstructionTerminatorInsideAttribute()
        {
            const string svg = "<svg data-note=\"?>\" viewBox=\"0 0 100 50\"><path d=\"M0 0 L100 50\"/></svg>";

            var actual = SvgPreviewHandlerHelper.AddStyleSVG(svg);

            Assert.StartsWith("<svg data-note=\"?>\" viewBox=\"0 0 100 50\" style=\"", actual);
            Assert.Contains("position: absolute; top: 50%; left: 50%;", actual);
            Assert.EndsWith("<path d=\"M0 0 L100 50\"/></svg>", actual);
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

        [TestMethod]
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
