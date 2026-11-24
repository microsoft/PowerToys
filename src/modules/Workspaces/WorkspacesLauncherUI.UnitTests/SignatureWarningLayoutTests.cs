// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class SignatureWarningLayoutTests
    {
        [DataTestMethod]
        [DataRow("SignatureWarningApp", "DisplayAppName")]
        [DataRow("SignatureWarningPath", "DisplayPath")]
        [DataRow("SignatureWarningArguments", "DisplayArguments")]
        [DataRow("SignatureWarningStatus", "DisplayStatus")]
        public void WarningAndAutomationUseEscapedRatherThanRawValues(string automationId, string property)
        {
            var field = ReadXaml().Descendants().Single(element => (string)element.Attribute("AutomationProperties.AutomationId") == automationId);
            StringAssert.Contains((string)field.Attribute("Text"), property);
            StringAssert.Contains((string)field.Attribute("AutomationProperties.Name"), property);
            Assert.AreEqual("Wrap", (string)field.Attribute("TextWrapping"));
            if (automationId != "SignatureWarningApp")
            {
                Assert.AreEqual("LeftToRight", (string)field.Attribute("FlowDirection"));
            }
        }

        [TestMethod]
        public void SafeChoiceIsEnabledAndRunRequiresExplicitClick()
        {
            var root = ReadXaml();
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var run = root.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "RunAnywayButton");
            var skip = root.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "SkipButton");
            Assert.AreEqual("False", (string)run.Attribute("IsEnabled"));
            Assert.AreEqual("RunAnyway_Click", (string)run.Attribute("Click"));
            Assert.AreEqual("Skip_Click", (string)skip.Attribute("Click"));
            Assert.IsNull(skip.Attribute("IsEnabled"));
            Assert.IsTrue(root.Descendants().Any(element => element.Name.LocalName == "ScrollViewer"));
            Assert.IsFalse(root.Descendants().Any(element => element.Name.LocalName == "TextBox"));
        }

        private static XDocument ReadXaml() => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestResources", "SignatureWarningWindow.xaml"));
    }
}
