// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    [TestClass]
    public class TimeDateCommandsProviderTests
    {
        private CultureInfo originalCulture = null!;
        private CultureInfo originalUiCulture = null!;

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
            Assert.AreEqual(2, commands.Length);
            Assert.IsNotNull(commands[0]);
            Assert.IsNotNull(commands[0].Title);
            Assert.IsNotNull(commands[0].Icon);
            Assert.AreEqual(Resources.timedate_custom_clocks_manage, commands[1].Title);
            Assert.IsNotNull(commands[1].Icon);
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
        public void GetDockBands_ReturnsNonEmptyArray()
        {
            var provider = new TimeDateCommandsProvider();

            var bands = provider.GetDockBands();

            Assert.IsTrue(bands.Length > 0, "GetDockBands should return at least one item");
            Assert.IsNotNull(bands[0], "First dock band should not be null");
        }

        [TestMethod]
        public void GetDockBands_NotificationCenterBandDoesNotSetDockIcon()
        {
            var provider = new TimeDateCommandsProvider();

            var bands = provider.GetDockBands();

            Assert.IsTrue(bands.Length > 1, "Expected notification center band to be present");
            Assert.IsNull(bands[1].Icon, "Notification center band should not set a dock icon");
        }

        [TestMethod]
        public async Task GetCommandItem_ConcurrentClockChangesDoNotThrow()
        {
            var clockFilePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
            var settingsFilePath = Path.Combine(Path.GetTempPath(), $"time-date-settings-{Guid.NewGuid()}.json");
            var clock = new CustomClock();

            try
            {
                var clockManager = new CustomClockManager(clockFilePath);
                clockManager.Save(clock);
                using var provider = new TimeDateCommandsProvider(
                    new SettingsManager(settingsFilePath),
                    clockManager,
                    new ClockUpdateService(enableTimer: false));
                using var start = new ManualResetEventSlim();
                var detailPageId = CustomClockIds.GetDetailPage(clock.Id);

                var writer = Task.Run(() =>
                {
                    start.Wait();
                    for (var index = 0; index < 100; index++)
                    {
                        clockManager.Save(new CustomClock { Id = clock.Id, Title = index.ToString(CultureInfo.InvariantCulture) });
                    }
                });
                var reader = Task.Run(() =>
                {
                    start.Wait();
                    for (var index = 0; index < 10_000; index++)
                    {
                        _ = provider.GetCommandItem(detailPageId);
                    }
                });

                start.Set();
                await Task.WhenAll(writer, reader);
            }
            finally
            {
                File.Delete(clockFilePath);
                File.Delete(settingsFilePath);
            }
        }
    }
}
