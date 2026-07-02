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

        [TestMethod]
        public void SubtitleShowsOnlyDateWhenWeekNumberSettingIsDisabled()
        {
            // Setup
            var settings = new Settings(showWeekNumberInClockBand: false);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedDate = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            Assert.AreEqual(expectedDate, band.Subtitle);
            Assert.AreEqual(2, band.MoreCommands.Length);
        }

        [TestMethod]
        public void SubtitleContainsWeekNumberWhenSettingIsEnabled()
        {
            // Setup
            var settings = new Settings(showWeekNumberInClockBand: true);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedWeek = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                DateTime.Now,
                DateTimeFormatInfo.CurrentInfo.CalendarWeekRule,
                DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);
            StringAssert.Contains(band.Subtitle, expectedWeek.ToString(CultureInfo.CurrentCulture));
            Assert.AreEqual(3, band.MoreCommands.Length);
        }

        [TestMethod]
        public void WeekNumberRespectsFirstWeekAndFirstDaySettings()
        {
            // Setup: ISO 8601 week numbering (first four day week, Monday as first day)
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, showWeekNumberInClockBand: true);

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
        public void CopyWeekNumberCommandHoldsWeekNumber()
        {
            // Setup
            var settings = new Settings(showWeekNumberInClockBand: true);

            // Act
            var band = new NowDockBand(settings);

            // Assert
            var expectedWeek = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                DateTime.Now,
                DateTimeFormatInfo.CurrentInfo.CalendarWeekRule,
                DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);
            var copyWeekItem = band.MoreCommands[2] as CommandContextItem;
            Assert.IsNotNull(copyWeekItem);
            var copyWeekCommand = copyWeekItem.Command as CopyTextCommand;
            Assert.IsNotNull(copyWeekCommand);
            Assert.AreEqual(expectedWeek.ToString(CultureInfo.CurrentCulture), copyWeekCommand.Text);
        }
    }
}
