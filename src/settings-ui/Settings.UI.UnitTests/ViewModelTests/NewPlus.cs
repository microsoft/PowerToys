// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class NewPlus
    {
        [TestMethod]
        public void CreateExplorerProcessStartInfoShouldQuoteTemplatePathAndUseFullExplorerPath()
        {
            // Arrange
            const string templatePath = @"C:\Users\Test User\Documents\My Templates";
            var createExplorerProcessStartInfoMethod = typeof(NewPlusViewModel).GetMethod("CreateExplorerProcessStartInfo", BindingFlags.NonPublic | BindingFlags.Static);

            // Act
            var processStartInfo = (ProcessStartInfo)createExplorerProcessStartInfoMethod.Invoke(null, new object[] { templatePath });

            // Assert
            Assert.IsNotNull(processStartInfo);
            Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), processStartInfo.FileName);
            Assert.AreEqual($"\"{templatePath}\"", processStartInfo.Arguments);
        }
    }
}
