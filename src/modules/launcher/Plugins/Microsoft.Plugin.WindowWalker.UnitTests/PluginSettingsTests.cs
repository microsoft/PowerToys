// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Plugin.WindowWalker.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.WindowWalker.UnitTests
{
    [TestClass]
    public class PluginSettingsTests
    {
        [TestMethod]
        public void SettingsCount()
        {
            // Setup
            PropertyInfo[] settings = WindowWalkerSettings.Instance?.GetType()?.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = settings?.Length;

            // Assert
            Assert.AreEqual(8, result);
        }

        [DataTestMethod]
        [DataRow("ResultsFromVisibleDesktopOnly")]
        [DataRow("SubtitleShowPid")]
        [DataRow("SubtitleShowDesktopName")]
        [DataRow("ConfirmKillProcess")]
        [DataRow("KillProcessTree")]
        [DataRow("OpenAfterKillAndClose")]
        [DataRow("HideKillProcessOnElevatedProcesses")]
        [DataRow("HideExplorerSettingInfo")]
        public void DoesSettingExist(string name)
        {
            // Setup
            Type settings = WindowWalkerSettings.Instance?.GetType();

            // Act
            var result = settings?.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);

            // Assert
            Assert.IsNotNull(result);
        }

        [DataTestMethod]
        [DataRow("ResultsFromVisibleDesktopOnly", false)]
        [DataRow("SubtitleShowPid", false)]
        [DataRow("SubtitleShowDesktopName", true)]
        [DataRow("ConfirmKillProcess", true)]
        [DataRow("KillProcessTree", false)]
        [DataRow("OpenAfterKillAndClose", false)]
        [DataRow("HideKillProcessOnElevatedProcesses", false)]
        [DataRow("HideExplorerSettingInfo", false)]
        public void DefaultValues(string name, bool valueExpected)
        {
            // Setup
            WindowWalkerSettings setting = WindowWalkerSettings.Instance;

            // Act
            PropertyInfo propertyInfo = setting?.GetType()?.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
            var result = propertyInfo?.GetValue(setting);

            // Assert
            Assert.AreEqual(valueExpected, result);
        }
    }
}
