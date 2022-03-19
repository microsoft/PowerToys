// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests
{
    [TestClass]
    public class PluginSettingsTests
    {
        [TestMethod]
        public void SettingsCount()
        {
            // Setup
            PropertyInfo[] settings = TimeDateSettings.Instance?.GetType()?.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = settings?.Length;

            // Assert
            Assert.AreEqual(4, result);
        }

        [DataTestMethod]
        [DataRow("OnlyDateTimeNowGlobal")]
        [DataRow("TimeWithSeconds")]
        [DataRow("DateWithWeekday")]
        [DataRow("HideNumberMessageOnGlobalQuery")]
        public void DoesSettingExist(string name)
        {
            // Setup
            Type settings = TimeDateSettings.Instance?.GetType();

            // Act
            var result = settings?.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);

            // Assert
            Assert.IsNotNull(result);
        }

        [DataTestMethod]
        [DataRow("OnlyDateTimeNowGlobal", true)]
        [DataRow("TimeWithSeconds", false)]
        [DataRow("DateWithWeekday", false)]
        [DataRow("HideNumberMessageOnGlobalQuery", false)]
        public void DefaultValues(string name, bool valueExpected)
        {
            // Setup
            TimeDateSettings setting = TimeDateSettings.Instance;

            // Act
            PropertyInfo propertyInfo = setting?.GetType()?.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
            var result = propertyInfo?.GetValue(setting);

            // Assert
            Assert.AreEqual(valueExpected, result);
        }
    }
}
