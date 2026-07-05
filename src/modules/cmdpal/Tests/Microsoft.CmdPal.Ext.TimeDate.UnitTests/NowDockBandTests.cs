// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    [TestClass]
    public class NowDockBandTests
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

        private static int CurrentWeek() => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
            DateTime.Now,
            DateTimeFormatInfo.CurrentInfo.CalendarWeekRule,
            DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);

        [TestMethod]
        public void SystemDateModeShowsOnlyTheDate()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 0);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedDate = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            Assert.AreEqual(expectedDate, band.Subtitle);
            Assert.AreEqual(2, band.MoreCommands.Length);
        }

        [TestMethod]
        public void WeekNumberModeAppendsTheWeekNumber()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 1);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            StringAssert.Contains(band.Subtitle, CurrentWeek().ToString(CultureInfo.CurrentCulture));
            Assert.AreEqual(3, band.MoreCommands.Length);
        }

        [TestMethod]
        public void WeekNumberModeRespectsFirstWeekAndFirstDaySettings()
        {
            // Setup: ISO 8601 week numbering (first four day week, Monday as first day)
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 1);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedWeek = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                DateTime.Now,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
            StringAssert.Contains(band.Subtitle, expectedWeek.ToString(CultureInfo.CurrentCulture));
        }

        [TestMethod]
        public void NumberOnlyModeAppendsTheBareWeekNumber()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 2);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedDate = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            Assert.AreEqual($"{expectedDate} · {CurrentWeek().ToString(CultureInfo.CurrentCulture)}", band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeOverridesTheDateLine()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 3, customDateFormatInClockBand: "yyyy-MM-dd");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            Assert.AreEqual(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture), band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeSupportsWeekOfYearPlaceholder()
        {
            // Setup: ISO 8601 week numbering
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 3, customDateFormatInClockBand: "\\W WOY");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedWeek = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                DateTime.Now,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
            Assert.AreEqual($"W {expectedWeek.ToString(CultureInfo.CurrentCulture)}", band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeWithEmptyFormatFallsBackToDefaultDate()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 3, customDateFormatInClockBand: string.Empty);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedDate = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            Assert.AreEqual(expectedDate, band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeWithInvalidFormatFallsBackToDefaultDate()
        {
            // Setup: an unclosed literal quote is an invalid .NET date format
            var settings = new Settings(clockBandDateMode: 3, customDateFormatInClockBand: "'unclosed");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedDate = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            Assert.AreEqual(expectedDate, band.Subtitle);
        }

        [TestMethod]
        public void ClickOpensNotificationCenterByDefault()
        {
            // Setup
            var settings = new Settings();

            // Act
            var band = new NowDockBand(settings);

            // Assert
            Assert.IsInstanceOfType(band.Command, typeof(OpenUrlCommand));
        }

        [TestMethod]
        public void ClickDoesNothingWhenNotificationCenterSettingIsDisabled()
        {
            // Setup
            var settings = new Settings(clockBandOpensNotificationCenter: false);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            Assert.IsInstanceOfType(band.Command, typeof(NoOpCommand));
        }

        [TestMethod]
        public void CopyWeekNumberCommandHoldsWeekNumber()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 1);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var copyWeekItem = band.MoreCommands[2] as CommandContextItem;
            Assert.IsNotNull(copyWeekItem);
            var copyWeekCommand = copyWeekItem.Command as CopyTextCommand;
            Assert.IsNotNull(copyWeekCommand);
            Assert.AreEqual(CurrentWeek().ToString(CultureInfo.CurrentCulture), copyWeekCommand.Text);
        }
    }
}
