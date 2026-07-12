// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TimeDate;
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
    public void CustomClockDisplay_RelativeDayIsRenderedAsLiteral()
    {
        var rendered = CustomClockDisplay.Format(DateTimeOffset.Now, "REL", new Settings());

        Assert.AreNotEqual("To12a26", rendered);
        Assert.IsFalse(string.IsNullOrEmpty(rendered));

        Assert.AreEqual(rendered, CustomClockDisplay.Format(DateTimeOffset.Now, "{relative}", new Settings()));
    }

    [DataTestMethod]
    [DataRow("T")]
    [DataRow("s")]
    [DataRow("R")]
    [DataRow("UXT")]
    [DataRow("UMS")]
    [DataRow("WFT")]
    public void CustomClockDisplay_SecondPrecisionFormatsRequireSecondUpdates(string format)
    {
        var clock = new CustomClock { TitleFormat = format };

        Assert.IsTrue(CustomClockDisplay.RequiresSecondUpdates(clock));
    }
}
