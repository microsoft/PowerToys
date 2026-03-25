// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    [TestClass]
    public class CalendarPageTests
    {
        private CultureInfo originalCulture;
        private CultureInfo originalUiCulture;

        [TestInitialize]
        public void Setup()
        {
            originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-us", false);
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us", false);
        }

        [TestCleanup]
        public void Cleanup()
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        [TestMethod]
        public void CalendarPage_HasExpectedId()
        {
            var page = new CalendarPage();

            Assert.AreEqual("com.microsoft.cmdpal.timedate.calendarPage", page.Id);
        }

        [TestMethod]
        public void CalendarPage_HasNonEmptyName()
        {
            var page = new CalendarPage();

            Assert.IsFalse(string.IsNullOrEmpty(page.Name));
        }

        [TestMethod]
        public void GetContent_ReturnsOneMarkdownContent()
        {
            var page = new CalendarPage();

            var content = page.GetContent();

            Assert.IsNotNull(content);
            Assert.AreEqual(1, content.Length);
            Assert.IsInstanceOfType(content[0], typeof(MarkdownContent));
        }

        [TestMethod]
        public void GetContent_TitleIsCurrentMonthAndYear()
        {
            var page = new CalendarPage();
            page.GetContent();

            var expected = DateTime.Now.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

            Assert.AreEqual(expected, page.Title);
        }

        [TestMethod]
        public void GetContent_BodyContainsTodayBold()
        {
            var page = new CalendarPage();
            var content = page.GetContent();

            var markdown = content[0] as MarkdownContent;
            var today = DateTime.Now.Day.ToString(CultureInfo.InvariantCulture);

            Assert.IsTrue(markdown!.Body.Contains($"**{today}**"), "Today's day number should be bold in the calendar markdown.");
        }

        [TestMethod]
        public void GetContent_BodyContainsDayHeaders()
        {
            var page = new CalendarPage();
            var content = page.GetContent();

            var markdown = content[0] as MarkdownContent;

            // The body should contain abbreviated day names as column headers
            var dayNames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
            foreach (var dayName in dayNames)
            {
                Assert.IsTrue(markdown!.Body.Contains(dayName), $"Calendar should contain day header '{dayName}'.");
            }
        }

        [TestMethod]
        public void GetContent_BodyContainsAllDaysOfCurrentMonth()
        {
            var page = new CalendarPage();
            var content = page.GetContent();

            var markdown = content[0] as MarkdownContent;
            var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);

            for (var day = 1; day <= daysInMonth; day++)
            {
                Assert.IsTrue(markdown!.Body.Contains(day.ToString(CultureInfo.InvariantCulture)), $"Calendar should contain day {day}.");
            }
        }

        [TestMethod]
        public void GetDockBands_CommandIsCalendarPage()
        {
            var provider = new TimeDateCommandsProvider();

            var bands = provider.GetDockBands();

            Assert.IsNotNull(bands);
            Assert.AreEqual(1, bands.Length);

            // The band wraps a list; dig into its items to find the NowDockBand command.
            var wrappedBand = bands[0] as WrappedDockItem;
            Assert.IsNotNull(wrappedBand, "Dock band should be a WrappedDockItem.");

            var items = wrappedBand.Items;
            Assert.IsTrue(items.Length > 0, "Dock band should have at least one item.");

            var command = items[0].Command;
            Assert.IsInstanceOfType(command, typeof(CalendarPage), "The clock dock item's command should be a CalendarPage.");
        }
    }
}
