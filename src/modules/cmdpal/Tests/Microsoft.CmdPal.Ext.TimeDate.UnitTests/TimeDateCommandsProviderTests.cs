// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    [TestClass]
    public class TimeDateCommandsProviderTests
    {
        private CultureInfo originalCulture;
        private CultureInfo originalUiCulture;

        [TestInitialize]
        public void Setup()
        {
            // Set culture to 'en-us'
            originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-us", false);
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us", false);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Restore original culture
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        [TestMethod]
        public void TimeDateCommandsProviderInitializationTest()
        {
            // Act
            var provider = new TimeDateCommandsProvider();

            // Assert
            Assert.IsNotNull(provider);
            Assert.IsNotNull(provider.DisplayName);
            Assert.AreEqual("com.microsoft.cmdpal.builtin.datetime", provider.Id);
            Assert.IsNotNull(provider.Icon);
            Assert.IsNotNull(provider.Settings);
        }

        [TestMethod]
        public void TopLevelCommandsTest()
        {
            // Setup
            var provider = new TimeDateCommandsProvider();

            // Act
            var commands = provider.TopLevelCommands();

            // Assert
            Assert.IsNotNull(commands);
            Assert.AreEqual(1, commands.Length);
            Assert.IsNotNull(commands[0]);
            Assert.IsNotNull(commands[0].Title);
            Assert.IsNotNull(commands[0].Icon);
        }

        [TestMethod]
        public void FallbackCommandsTest()
        {
            // Setup
            var provider = new TimeDateCommandsProvider();

            // Act
            var fallbackCommands = provider.FallbackCommands();

            // Assert
            Assert.IsNotNull(fallbackCommands);
            Assert.AreEqual(1, fallbackCommands.Length);
            Assert.IsNotNull(fallbackCommands[0]);
        }

        [TestMethod]
        public void DisplayNameTest()
        {
            // Setup
            var provider = new TimeDateCommandsProvider();

            // Act
            var displayName = provider.DisplayName;

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(displayName));
        }

        [TestMethod]
        public void GetTranslatedPluginDescriptionTest()
        {
            // Setup
            var provider = new TimeDateCommandsProvider();

            // Act
            var commands = provider.TopLevelCommands();
            var subtitle = commands[0].Subtitle;

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(subtitle));
            Assert.IsTrue(subtitle.Contains("Show time and date values in different formats"));
        }
    }
}
