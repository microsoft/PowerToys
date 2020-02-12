using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SvgPreviewHandler.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace UnitTests_SvgPreviewHandler
{
    [TestClass]
    public class SvgPreviewHandlerHelperTests
    {
        [DataTestMethod]
        [DataRow("script")]
        [DataRow("image")]
        public void RemoveElements_ShoudSetfoundBlockedElementTrue_IfBlockedElementFound(string element) 
        {
            // Arrange
            var xmlTree = new XElement("svg",
                new XElement(element, "valid")
            );
            var svgData = xmlTree.ToString();
            bool foundFilteredElement;

            // Act
            SvgPreviewHandlerHelper.RemoveElements(svgData, out foundFilteredElement);

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void RemoveElements_ShoudSetfoundBlockedElementFalse_IfNoBlockedElementFound()
        {
            // Arrange
            var xmlTree = new XElement("svg",
                new XElement("validElement", "valid")
            );
            var svgData = xmlTree.ToString();
            bool foundFilteredElement;

            // Act
            SvgPreviewHandlerHelper.RemoveElements(svgData, out foundFilteredElement);

            // Assert
            Assert.IsFalse(foundFilteredElement);
        }

        [TestMethod]
        public void RemoveElements_ShoudRemoveBlockedElements_IfPresent()
        {
            // Arrange
            var xmlTree = new XElement("svg",
                new XElement("script", "valid")
            );
            var svgData = xmlTree.ToString();
            var expectedXmlTree = new XElement("svg");
            var expectedSvgData = expectedXmlTree.ToString();
            bool foundFilteredElement;

            // Act
            var actualSvgData = SvgPreviewHandlerHelper.RemoveElements(svgData, out foundFilteredElement);

            // Assert
            Assert.AreEqual(expectedSvgData, actualSvgData);
        }

        [TestMethod]
        public void RemoveElements_ShoudRemoveBlockedElements_IfPresentAtSubChildLevel()
        {
            // Arrange
            var xmlTree = new XElement("svg",
                new XElement("valid", new XElement("script", "valid-content"))
            );
            var svgData = xmlTree.ToString();
            var expectedXmlTree = new XElement("svg", new XElement("valid"));
            var expectedSvgData = expectedXmlTree.ToString();
            bool foundFilteredElement;

            // Act
            var actualSvgData = SvgPreviewHandlerHelper.RemoveElements(svgData, out foundFilteredElement);

            // Assert
            Assert.AreEqual(expectedSvgData, actualSvgData);
        }

        [TestMethod]
        public void RemoveElements_ShoudRemoveBlockedElementsAndChildElement_IfPresent()
        {
            // Arrange
            var xmlTree = new XElement("svg",
                new XElement("script", new XElement("validChild1", new XElement("validChild2", "valid")))
            );
            var svgData = xmlTree.ToString();
            var expectedXmlTree = new XElement("svg");
            var expectedSvgData = expectedXmlTree.ToString();
            bool foundFilteredElement;

            // Act
            var actualSvgData = SvgPreviewHandlerHelper.RemoveElements(svgData, out foundFilteredElement);

            // Assert
            Assert.AreEqual(expectedSvgData, actualSvgData);
        }

        [TestMethod]
        public void RemoveElements_ShoudRemoveAllBlockedElements_IfMultipleElementsArePresent()
        {
            // Arrange
            var xmlTree = new XElement("svg",
                new XElement("script", "valid-script-1"),
                new XElement("script", "valid-script-2"),
                new XElement("image", "valid-image"),
                new XElement("valid-element", "valid")
            );
            var svgData = xmlTree.ToString();
            var expectedXmlTree = new XElement("svg",
                new XElement("valid-element", "valid")
            );
            var expectedSvgData = expectedXmlTree.ToString();
            bool foundFilteredElement;

            // Act
            var actualSvgData = SvgPreviewHandlerHelper.RemoveElements(svgData, out foundFilteredElement);

            // Assert
            Assert.AreEqual(expectedSvgData, actualSvgData);
        }
    }
}
