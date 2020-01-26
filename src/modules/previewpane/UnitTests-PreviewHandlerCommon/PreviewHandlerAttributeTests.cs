// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests_PreviewHandlerCommon
{
    [TestClass]
    public class PreviewHandlerAttributeTests
    {
        [PreviewHandler("valid-name", "valid-extension", "valid-appid")]
        private class TestPreviewHandler 
        {
        }


        [DataTestMethod]
        [DataRow("name")]
        [DataRow("extension")]
        [DataRow("appId")]
        public void PreviewHandlerAttributes_ShouldThrow_IfInitializeWithNullArguments(string argName)
        {
            // Arrange
            var name = (argName.Equals("name")) ? null : string.Empty;
            var extension = (argName.Equals("extension")) ? null : string.Empty;
            var appId = (argName.Equals("appId")) ? null : string.Empty;
            ArgumentNullException exception = null;

            // Act
            try
            {
                var previewHandlerAttribute = new PreviewHandlerAttribute(name, extension, appId);
            }
            catch (ArgumentNullException ex)
            {
                exception = ex;
            }

            // Assert
            Assert.IsNotNull(exception);
        }

        [TestMethod]
        public void PreviewHandlerAttributes_ShouldSetValidFields_IfInitializeWithValidArguments()
        {
            // Arrange
            var name = "valid-name";
            var extension = "valid-extension";
            var appId = "valid-appid";

            // Act
            var previewHandlerAttribute = new PreviewHandlerAttribute(name, extension, appId);

            // Assert
            Assert.AreEqual(name, previewHandlerAttribute.Name);
            Assert.AreEqual(extension, previewHandlerAttribute.Extension);
            Assert.AreEqual(appId, previewHandlerAttribute.AppId);
        }

        [TestMethod]
        public void PreviewHandlerAttributes_ShouldSetValidFields_WhenInitializeFromAttributes()
        {
            // Arrange
            var testPreviewHandler = new TestPreviewHandler();

            // Act
            var attr = (object[])testPreviewHandler.GetType().GetCustomAttributes(typeof(PreviewHandlerAttribute));
            var previewHandlerAttributes = attr[0] as PreviewHandlerAttribute;

            // Assert
            Assert.AreEqual("valid-name", previewHandlerAttributes.Name);
            Assert.AreEqual("valid-extension", previewHandlerAttributes.Extension);
            Assert.AreEqual("valid-appid", previewHandlerAttributes.AppId);
        }
    }
}
