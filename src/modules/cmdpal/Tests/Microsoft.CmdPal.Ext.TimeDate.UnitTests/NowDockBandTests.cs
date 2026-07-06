// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
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

        // Default numbering system is ISO 8601.
        private static int CurrentWeek() => ISOWeek.GetWeekOfYear(DateTime.Now);

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
        public void WeekNumberModeRespectsCustomFirstWeekAndFirstDaySettings()
        {
            // Setup: custom week mode with first day rule and Monday
            var settings = new Settings(firstWeekOfYear: 0, firstDayOfWeek: 1, clockBandDateMode: 3);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedWeek = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                DateTime.Now,
                CalendarWeekRule.FirstDay,
                DayOfWeek.Monday);
            StringAssert.Contains(band.Subtitle, expectedWeek.ToString(CultureInfo.CurrentCulture));
        }

        [TestMethod]
        public void IsoWeekDateModeShowsTheIsoWeekDate()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 4);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            Assert.AreEqual(TimeAndDateHelper.GetIsoWeekDateString(DateTime.Now), band.Subtitle);
            Assert.AreEqual(3, band.MoreCommands.Length);
        }

        [TestMethod]
        public void UsWeekModeAppendsTheUsWeekNumber()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 2);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                DateTime.Now,
                CalendarWeekRule.FirstDay,
                DayOfWeek.Sunday);
            StringAssert.Contains(band.Subtitle, expectedWeek.ToString(CultureInfo.CurrentCulture));
            Assert.AreEqual(3, band.MoreCommands.Length);
        }

        [TestMethod]
        public void CustomFormatModeOverridesTheDateLine()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 5, customDateFormatInClockBand: "yyyy-MM-dd");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            Assert.AreEqual(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture), band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeSupportsWeekOfYearPlaceholder()
        {
            // Setup: ISO-like first week/first day settings feed the WOY placeholder
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 5, customDateFormatInClockBand: "\\W WOY");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            Assert.AreEqual($"W {CurrentWeek().ToString(CultureInfo.CurrentCulture)}", band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeWithEmptyFormatFallsBackToDefaultDate()
        {
            // Setup
            var settings = new Settings(clockBandDateMode: 5, customDateFormatInClockBand: string.Empty);

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
            var settings = new Settings(clockBandDateMode: 5, customDateFormatInClockBand: "'unclosed");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedDate = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            Assert.AreEqual(expectedDate, band.Subtitle);
        }

        [TestMethod]
        public void CustomFormatModeRendersUnrecognizedLettersLiterally()
        {
            // 'W' is no date format specifier and is copied to the output unchanged, so
            // a German user can write '\KW WOY' to get 'KW 27' without escaping the W.
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 5, customDateFormatInClockBand: "dd.MM \\KW WOY");

            // Act
            var band = new NowDockBand(settings);

            // Assert
            StringAssert.Contains(band.Subtitle, "KW ");
            StringAssert.Contains(band.Subtitle, CurrentWeek().ToString(CultureInfo.CurrentCulture));
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
