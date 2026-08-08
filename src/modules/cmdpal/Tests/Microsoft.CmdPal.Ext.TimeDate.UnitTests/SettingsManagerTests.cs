// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class SettingsManagerTests
{
    [TestMethod]
    public void SaveSettings_PersistsRegisteredAndAdditionalSettingsTogether()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"time-date-settings-{Guid.NewGuid()}.json");

        try
        {
            var settings = new SettingsManager(filePath);
            settings.Settings.Update("""{"timeDate.TimeWithSecond":"true"}""");
            settings.SetDockClockFormats("T", "REL", "s");

            var savedSettings = JsonNode.Parse(File.ReadAllText(filePath))!.AsObject();
            Assert.AreEqual("true", savedSettings["timeDate.TimeWithSecond"]!.GetValue<string>());
            Assert.AreEqual("T", savedSettings["timeDate.DockClockTitleFormat"]!.GetValue<string>());
            Assert.AreEqual("REL", savedSettings["timeDate.DockClockSubtitleFormat"]!.GetValue<string>());
            Assert.AreEqual("s", savedSettings["timeDate.DockClockCopyFormat"]!.GetValue<string>());

            var reloadedSettings = new SettingsManager(filePath);
            Assert.IsTrue(reloadedSettings.TimeWithSecond);
            Assert.AreEqual("T", reloadedSettings.DockClockTitleFormat);
            Assert.AreEqual("REL", reloadedSettings.DockClockSubtitleFormat);
            Assert.AreEqual("s", reloadedSettings.DockClockCopyFormat);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SetDockClockFormats_InvalidFormatDoesNotChangeSettingsOrRaiseEvent()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"time-date-settings-{Guid.NewGuid()}.json");

        try
        {
            var settings = new SettingsManager(filePath);
            settings.SetDockClockFormats("T", "REL", "s");
            var formatsChanged = false;
            settings.DockClockFormatsChanged += (_, _) => formatsChanged = true;

            Assert.ThrowsException<ArgumentException>(() => settings.SetDockClockFormats("UTC:%", "d", string.Empty));
            Assert.AreEqual("T", settings.DockClockTitleFormat);
            Assert.AreEqual("REL", settings.DockClockSubtitleFormat);
            Assert.AreEqual("s", settings.DockClockCopyFormat);
            Assert.IsFalse(formatsChanged);

            var savedSettings = JsonNode.Parse(File.ReadAllText(filePath))!.AsObject();
            Assert.AreEqual("T", savedSettings["timeDate.DockClockTitleFormat"]!.GetValue<string>());
            Assert.AreEqual("REL", savedSettings["timeDate.DockClockSubtitleFormat"]!.GetValue<string>());
            Assert.AreEqual("s", savedSettings["timeDate.DockClockCopyFormat"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void LoadSettings_InvalidDockFormatFallsBackWithoutDiscardingValidFormat()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"time-date-settings-{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, """{"timeDate.DockClockTitleFormat":"UTC:%","timeDate.DockClockSubtitleFormat":"REL","timeDate.DockClockCopyFormat":"UTC:%"}""");

        try
        {
            var settings = new SettingsManager(filePath);

            Assert.AreEqual("t", settings.DockClockTitleFormat);
            Assert.AreEqual("REL", settings.DockClockSubtitleFormat);
            Assert.AreEqual(string.Empty, settings.DockClockCopyFormat);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void EditDefaultDockClockForm_InvalidSubmissionKeepsFormOpenAndSettingsUnchanged()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"time-date-settings-{Guid.NewGuid()}.json");

        try
        {
            var settings = new SettingsManager(filePath);
            var form = new EditDefaultDockClockForm(settings);

            var malformedResult = form.SubmitForm("{");
            var invalidFormatResult = form.SubmitForm("""{"titleFormat":"UTC:%","subtitleFormat":"d"}""");

            Assert.AreEqual(CommandResultKind.KeepOpen, malformedResult.Kind);
            Assert.AreEqual(CommandResultKind.KeepOpen, invalidFormatResult.Kind);
            Assert.AreEqual("t", settings.DockClockTitleFormat);
            Assert.AreEqual("d", settings.DockClockSubtitleFormat);
            Assert.IsFalse(File.Exists(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void EditDefaultDockClockForm_ValidSubmissionSavesOptionalCopyFormat()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"time-date-settings-{Guid.NewGuid()}.json");

        try
        {
            var settings = new SettingsManager(filePath);
            var form = new EditDefaultDockClockForm(settings);

            var result = form.SubmitForm("""{"titleFormat":"T","subtitleFormat":"REL","copyFormat":"s"}""");

            Assert.AreEqual(CommandResultKind.GoBack, result.Kind);
            Assert.AreEqual("s", settings.DockClockCopyFormat);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
