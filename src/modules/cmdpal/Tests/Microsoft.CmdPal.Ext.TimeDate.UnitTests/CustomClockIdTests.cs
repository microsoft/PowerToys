// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
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
    public void CustomClockDisplay_RelativeDayTokenIsRenderedAsLiteral()
    {
        var now = DateTimeOffset.Now;
        var rendered = CustomClockDisplay.Format(now, "REL", new Settings());

        Assert.AreNotEqual("To12a26", rendered);
        Assert.IsFalse(string.IsNullOrEmpty(rendered));
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
}
