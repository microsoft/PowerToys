// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class CustomClockIdTests
{
    [TestMethod]
    public void CustomClockSurfaceIds_AreDistinct()
    {
        var clockId = Guid.NewGuid();

        Assert.AreNotEqual(CustomClockIds.GetDetailPage(clockId), CustomClockIds.GetDockBand(clockId));
        Assert.AreNotEqual(CustomClockIds.LocalDetailPage, CustomClockIds.GetDetailPage(clockId));
        Assert.AreNotEqual(CustomClockIds.LocalDetailPage, CustomClockIds.GetDockBand(clockId));
    }

    [TestMethod]
    public void EditCustomClockPage_ContainsValidAdaptiveCardJson()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var page = new EditCustomClockPage(new CustomClockManager(statePath), new Settings(), null);

        var form = page.GetContent()[0] as FormContent;

        Assert.IsNotNull(form);
        Assert.IsNotNull(JsonNode.Parse(form.TemplateJson));
    }

    [TestMethod]
    public void CustomClockListPage_ImplementsDynamicSearchContract()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        using var updateService = new ClockUpdateService(enableTimer: false);
        using var page = new CustomClockListPage(new CustomClockManager(statePath), new Settings(), updateService);
        var dynamicPage = page as IDynamicListPage;
        var itemsChanged = false;
        page.ItemsChanged += (_, _) => itemsChanged = true;

        Assert.IsNotNull(dynamicPage);
        dynamicPage.SearchText = "search";

        Assert.AreEqual("search", page.SearchText);
        Assert.IsTrue(itemsChanged);
    }

    [TestMethod]
    public void CustomClockListPage_LoadedStartsUpdatingFetchedItemsWithoutReplacingThem()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var manager = new CustomClockManager(statePath);
        manager.Save(new CustomClock());

        try
        {
            using var updateService = new ClockUpdateService(enableTimer: false);
            using var page = new CustomClockListPage(manager, new Settings(), updateService);
            var fetchedItems = page.GetItems();
            global::Windows.Foundation.TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };

            page.ItemsChanged += handler;
            var loadedItems = page.GetItems();
            page.ItemsChanged -= handler;

            Assert.AreSame(fetchedItems[3], loadedItems[3]);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [TestMethod]
    public void CustomClockOverviewItem_StartUpdatingRefreshesTextImmediately()
    {
        using var updateService = new ClockUpdateService(enableTimer: false);
        var item = new CustomClockOverviewItem(new CustomClock(), new Settings(), updateService)
        {
            Title = "stale",
        };

        item.StartUpdating();

        Assert.AreNotEqual("stale", item.Title);
        item.StopUpdating();
    }

    [TestMethod]
    public void CustomClockOverviewItem_ContextMenuIncludesDisplayAndOptionalCopyCommands()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var clock = new CustomClock { TitleFormat = "t", SubtitleFormat = "d", CopyFormat = "s" };
        using var updateService = new ClockUpdateService(enableTimer: false);
        var item = new CustomClockOverviewItem(clock, new Settings(), updateService);

        item.AddManagementCommands(new CustomClockManager(statePath));

        Assert.AreEqual(item.CopyTitleCommand.Text, CustomClockDisplay.Format(CustomClockDisplay.GetCurrentTime(TimeZoneInfo.Local), "t", new Settings()));
        Assert.AreEqual(item.CopySubtitleCommand.Text, CustomClockDisplay.Format(CustomClockDisplay.GetCurrentTime(TimeZoneInfo.Local), "d", new Settings()));
        Assert.IsNotNull(item.CopyCustomFormatCommand);
        Assert.AreEqual(5, item.MoreCommands.Length);
    }

    [DataTestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE")]
    public void CustomClockFormatOptions_AppendExampleUsingCurrentCulture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var option = CustomClockFormatOptions.Get(new Settings()).Single(candidate => candidate.Value == "d");
            var exampleDateTime = new DateTimeOffset(2000, 1, 2, 15, 4, 5, TimeSpan.FromHours(2));
            var expected = CustomClockDisplay.Format(exampleDateTime, "d", new Settings());

            StringAssert.EndsWith(option.Title, $"({expected})");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void CustomClockFormatOptions_InvalidCustomFormatDoesNotPreventFormCreation()
    {
        var settings = new Settings(customFormats: ["Invalid=UTC:%"]);

        var option = CustomClockFormatOptions.Get(settings).Single(candidate => candidate.Value == "UTC:%");

        Assert.AreEqual("Invalid", option.Title);
        Assert.IsNotNull(new EditCustomClockPage(new CustomClockManager(Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json")), settings, null));
    }

    [TestMethod]
    public void CustomClockFormatOptions_CopyCommandUsesSelectedCustomFormatTitle()
    {
        var settings = new Settings(customFormats: ["Sortable=yyyy-MM-dd"]);

        var commandName = CustomClockFormatOptions.GetCopyCommandName(settings, "yyyy-MM-dd");

        StringAssert.StartsWith(commandName, "Copy Sortable (");
    }

    [TestMethod]
    public void CustomClockFormatOptions_CopyCommandPreservesCustomFormatNameCasing()
    {
        var settings = new Settings(customFormats: ["PowerToys time=HH:mm"]);

        var commandName = CustomClockFormatOptions.GetCopyCommandName(settings, "HH:mm");

        StringAssert.StartsWith(commandName, "Copy PowerToys time (");
    }

    [TestMethod]
    public void CustomClockDisplay_RelativeDayTokenIsRenderedAsLiteral()
    {
        var now = DateTimeOffset.Now;
        var rendered = CustomClockDisplay.Format(now, "REL", new Settings());

        Assert.AreNotEqual("To12a26", rendered);
        Assert.IsFalse(string.IsNullOrEmpty(rendered));
    }

    [TestMethod]
    public void CustomClockDisplay_EscapedRelativeDayTokenIsRenderedLiterally()
    {
        var rendered = CustomClockDisplay.Format(DateTimeOffset.Now, @"\REL", new Settings());

        Assert.AreEqual("REL", rendered);
    }

    [TestMethod]
    public void CustomClockDisplay_StandardFormatMatchesDateTimeFormatting()
    {
        var time = new DateTimeOffset(2025, 7, 1, 14, 5, 6, TimeSpan.FromHours(2));
        const string format = "yyyy-MM-dd HH:mm";

        var rendered = CustomClockDisplay.Format(time, format, new Settings());

        Assert.AreEqual(time.DateTime.ToString(format, CultureInfo.CurrentCulture), rendered);
    }

    [TestMethod]
    public void CustomClockDisplay_UtcFormatUsesUtcTime()
    {
        var time = new DateTimeOffset(2025, 7, 1, 14, 5, 6, TimeSpan.FromHours(2));
        const string format = "yyyy-MM-dd HH:mm";

        var rendered = CustomClockDisplay.Format(time, $"UTC:{format}", new Settings());

        Assert.AreEqual(time.UtcDateTime.ToString(format, CultureInfo.CurrentCulture), rendered);
    }

    [TestMethod]
    public void CustomClockDisplay_EmbeddedCustomTokensAreRendered()
    {
        var time = new DateTimeOffset(2025, 7, 1, 14, 5, 6, TimeSpan.FromHours(2));
        var settings = new Settings();
        var calendar = CultureInfo.CurrentCulture.Calendar;
        var firstDay = TimeAndDateHelper.GetFirstDayOfWeek(settings.FirstDayOfWeek);
        var rule = TimeAndDateHelper.GetCalendarWeekRule(settings.FirstWeekOfYear);
        var expectedWeek = calendar.GetWeekOfYear(time.DateTime, rule, firstDay);
        var expectedUnixTime = time.ToUnixTimeSeconds();

        var rendered = CustomClockDisplay.Format(time, "yyyy WOY UXT", settings);

        Assert.AreEqual($"2025 {expectedWeek} {expectedUnixTime}", rendered);
    }

    [TestMethod]
    public void CustomClockDisplay_CachedExplicitZoneAppliesDaylightSavingRules()
    {
        var clock = new CustomClock { TimeZoneId = "Pacific Standard Time" };
        var timeZone = CustomClockDisplay.ResolveExplicitTimeZone(clock);

        Assert.IsNotNull(timeZone);
        var winter = CustomClockDisplay.GetCurrentTime(timeZone, new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var summer = CustomClockDisplay.GetCurrentTime(timeZone, new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(TimeSpan.FromHours(-8), winter.Offset);
        Assert.AreEqual(TimeSpan.FromHours(-7), summer.Offset);
    }

    [DataTestMethod]
    [DataRow("T")]
    [DataRow("s")]
    [DataRow("R")]
    [DataRow("UXT")]
    [DataRow("UMS")]
    [DataRow("WFT")]
    [DataRow("yyyy UXT")]
    [DataRow("yyyy UMS")]
    [DataRow("yyyy WFT")]
    [DataRow("OAD")]
    [DataRow("EXC")]
    [DataRow("EXF")]
    public void CustomClockDisplay_SecondPrecisionFormatsRequireSecondUpdates(string format)
    {
        var clock = new CustomClock { TitleFormat = format };

        Assert.IsTrue(CustomClockDisplay.RequiresSecondUpdates(clock));
    }

    [DataTestMethod]
    [DataRow("yyyy-MM-dd HH:mm")]
    [DataRow("HH:mm 'seconds'")]
    [DataRow("HH:mm \"seconds\"")]
    public void CustomClockDisplay_FormatsWithoutActiveSecondTokensDoNotRequireSecondUpdates(string format)
    {
        var clock = new CustomClock { TitleFormat = format, SubtitleFormat = string.Empty };

        Assert.IsFalse(CustomClockDisplay.RequiresSecondUpdates(clock));
    }

    [DataTestMethod]
    [DataRow("%")]
    [DataRow("UTC:%")]
    [DataRow("WOY %")]
    public void CustomClockManager_SaveRejectsInvalidFormats(string format)
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var manager = new CustomClockManager(statePath);
        var clock = new CustomClock { TitleFormat = format };

        Assert.ThrowsException<ArgumentException>(() => manager.Save(clock));
    }

    [TestMethod]
    public void CustomClockManager_SaveRejectsInvalidCopyFormat()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var manager = new CustomClockManager(statePath);

        Assert.ThrowsException<ArgumentException>(() => manager.Save(new CustomClock { CopyFormat = "UTC:%" }));
    }

    [TestMethod]
    public void CustomClockDockBand_CopyCommandsUseDisplayedAndOptionalFormats()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var clock = new CustomClock { TitleFormat = "t", SubtitleFormat = "d", CopyFormat = "s" };
        var utcNow = new DateTime(2025, 7, 1, 12, 5, 32, DateTimeKind.Utc);
        using var updateService = new ClockUpdateService(enableTimer: false);
        using var band = new CustomClockDockBand(clock, new CustomClockManager(statePath), new Settings(), updateService, () => utcNow);

        Assert.AreEqual(band.Title, band.CopyTitleCommand.Text);
        Assert.AreEqual(band.Subtitle, band.CopySubtitleCommand.Text);
        StringAssert.StartsWith(band.CopyTitleCommand.Name, "Copy time (");
        StringAssert.StartsWith(band.CopySubtitleCommand.Name, "Copy date (");
        Assert.IsNotNull(band.CopyCustomFormatCommand);
        var localTime = CustomClockDisplay.GetCurrentTime(TimeZoneInfo.Local, new DateTimeOffset(utcNow));
        Assert.AreEqual(CustomClockDisplay.Format(localTime, "s", new Settings()), band.CopyCustomFormatCommand.GetCurrentText());
        StringAssert.StartsWith(band.CopyCustomFormatCommand.Name, "Copy ISO 8601");
    }

    [TestMethod]
    public void CustomClockManager_LoadSkipsInvalidTimeZoneWithoutDiscardingValidClocks()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");
        var validClock = new CustomClock { Title = "Valid" };
        var invalidClock = new CustomClock { Title = "Invalid", TimeZoneId = "Invalid time zone" };
        File.WriteAllText(
            statePath,
            JsonSerializer.Serialize(new List<CustomClock> { validClock, invalidClock }, CustomClockJsonContext.Default.ListCustomClock));

        try
        {
            var manager = new CustomClockManager(statePath);

            Assert.AreEqual(1, manager.Clocks.Count);
            Assert.AreEqual(validClock.Id, manager.Clocks[0].Id);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [TestMethod]
    public void EditCustomClockForm_InvalidSubmissionsKeepFormOpenWithoutSaving()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");

        try
        {
            var manager = new CustomClockManager(statePath);
            var form = new EditCustomClockForm(manager, new Settings(), null);

            var malformedResult = form.SubmitForm("{");
            var invalidTimeZoneResult = form.SubmitForm("""{"timeZoneId":"Invalid time zone","titleFormat":"t","subtitleFormat":"d"}""");

            Assert.AreEqual(CommandResultKind.KeepOpen, malformedResult.Kind);
            Assert.AreEqual(CommandResultKind.KeepOpen, invalidTimeZoneResult.Kind);
            Assert.AreEqual(0, manager.Clocks.Count);
            Assert.IsFalse(File.Exists(statePath));
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [TestMethod]
    public void EditCustomClockForm_ValidSubmissionSavesOptionalCopyFormat()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"custom-clocks-{Guid.NewGuid()}.json");

        try
        {
            var manager = new CustomClockManager(statePath);
            var form = new EditCustomClockForm(manager, new Settings(), null);

            var result = form.SubmitForm("""{"timeZoneId":"current","titleFormat":"t","subtitleFormat":"d","copyFormat":"s"}""");

            Assert.AreEqual(CommandResultKind.GoHome, result.Kind);
            Assert.AreEqual("s", manager.Clocks.Single().CopyFormat);
        }
        finally
        {
            File.Delete(statePath);
        }
    }
}
