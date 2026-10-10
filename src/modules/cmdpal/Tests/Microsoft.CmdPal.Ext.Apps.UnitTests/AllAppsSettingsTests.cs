// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsSettingsTests
{
    [TestMethod]
    [DataRow("apps.ScanAllDesktopSubfolders")]
    [DataRow("apps.ScanAllCustomShortcutSubfolders")]
    public void ScanAllSubfolders_LabelsUseDefaultDepthWithoutSubtitles(string settingKey)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            var defaultDepth = settingKey == "apps.ScanAllDesktopSubfolders"
                ? settings.DesktopMaximumDepth
                : settings.CustomShortcutFolderMaximumDepth;
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            var body = JsonNode.Parse(form.TemplateJson)!["body"]!.AsArray();
            var toggle = body.Single(item => item?["columns"]?[0]?["items"]?[0]?["id"]?.GetValue<string>() == settingKey);
            var labelItems = toggle!["columns"]![1]!["items"]!.AsArray();

            Assert.AreEqual(1, labelItems.Count, "Depth toggles should render their label without a subtitle.");
            var label = labelItems[0]!["text"]!.GetValue<string>();
            StringAssert.Contains(label, defaultDepth.ToString(CultureInfo.CurrentCulture));
            Assert.IsFalse(label.Contains("{0}", StringComparison.Ordinal));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void ScanAllSubfolders_FormSubmissionPersistsIndependentSettings(bool scanAllDesktopSubfolders, bool scanAllCustomShortcutSubfolders)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            Assert.IsFalse(settings.ScanAllDesktopSubfolders);
            Assert.IsFalse(settings.ScanAllCustomShortcutSubfolders);
            Assert.AreEqual(3, settings.DesktopMaximumDepth);
            Assert.AreEqual(3, settings.CustomShortcutFolderMaximumDepth);
            var form = (SettingsForm)settings.Settings.ToContent().Single();

            form.SubmitForm(
                new JsonObject
                {
                    ["apps.ScanAllDesktopSubfolders"] = scanAllDesktopSubfolders ? "true" : "false",
                    ["apps.ScanAllCustomShortcutSubfolders"] = scanAllCustomShortcutSubfolders ? "true" : "false",
                }.ToJsonString(),
                string.Empty);

            var reloaded = new AllAppsSettings(settingsPath);
            Assert.AreEqual(scanAllDesktopSubfolders, settings.ScanAllDesktopSubfolders);
            Assert.AreEqual(scanAllCustomShortcutSubfolders, settings.ScanAllCustomShortcutSubfolders);
            Assert.AreEqual(scanAllDesktopSubfolders, reloaded.ScanAllDesktopSubfolders);
            Assert.AreEqual(scanAllCustomShortcutSubfolders, reloaded.ScanAllCustomShortcutSubfolders);
            Assert.AreEqual(scanAllDesktopSubfolders ? int.MaxValue : 3, reloaded.DesktopMaximumDepth);
            Assert.AreEqual(scanAllCustomShortcutSubfolders ? int.MaxValue : 3, reloaded.CustomShortcutFolderMaximumDepth);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("unknown")]
    [DataRow("default")]
    public void ExecutableNameMatchMode_DefaultsForMissingOrUnknownSavedValues(string savedValue)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            if (!string.IsNullOrEmpty(savedValue))
            {
                File.WriteAllText(
                    settingsPath,
                    new JsonObject
                    {
                        ["apps.ExecutableNameMatchMode"] = savedValue,
                    }.ToJsonString());
            }

            var settings = new AllAppsSettings(settingsPath);

            Assert.AreEqual(ExecutableNameMatchMode.FilenameOnly, settings.ExecutableNameMatchMode);
            Assert.AreEqual("default", JsonNode.Parse(settings.Settings.ToJson())!["apps.ExecutableNameMatchMode"]!.GetValue<string>());
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow("default", ExecutableNameMatchMode.FilenameOnly)]
    [DataRow("filenameAndStem", ExecutableNameMatchMode.FilenameAndStem)]
    [DataRow("filenameOnly", ExecutableNameMatchMode.FilenameOnly)]
    [DataRow("disabled", ExecutableNameMatchMode.Disabled)]
    public void ExecutableNameMatchMode_FormSubmissionPersists(string value, ExecutableNameMatchMode expected)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{\"apps.ExecutableNameMatchMode\":\"filenameOnly\"}");
            var settings = new AllAppsSettings(settingsPath);
            var form = (SettingsForm)settings.Settings.ToContent().Single();

            form.SubmitForm(
                new JsonObject
                {
                    ["apps.ExecutableNameMatchMode"] = value,
                }.ToJsonString(),
                string.Empty);

            Assert.AreEqual(expected, settings.ExecutableNameMatchMode);
            Assert.AreEqual(expected, new AllAppsSettings(settingsPath).ExecutableNameMatchMode);
            Assert.AreEqual(value, JsonNode.Parse(File.ReadAllText(settingsPath))!["apps.ExecutableNameMatchMode"]!.GetValue<string>());
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void ExclusionPatterns_FormSubmissionPersists()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            var values = new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create("*Updater*")).ToJsonString(),
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(@"C:\Tools\*")).ToJsonString(),
            };
            form.SubmitForm(values.ToJsonString(), string.Empty);

            var reloaded = new AllAppsSettings(settingsPath);
            Assert.AreEqual("*Updater*", reloaded.ExcludedAppNames.Single());
            Assert.AreEqual(@"C:\Tools\*", reloaded.ExcludedAppPaths.Single());

            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            Assert.AreEqual("*Updater*", settings.ExcludedAppNames.Single());
            Assert.AreEqual(@"C:\Tools\*", settings.ExcludedAppPaths.Single());

            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[]\"}", string.Empty);
            Assert.AreEqual(0, new AllAppsSettings(settingsPath).ExcludedAppNames.Count);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_InvalidJsonObjectKeepsDefaultsAndFileContents()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "[]");
            var settings = new AllAppsSettings(settingsPath);

            Assert.AreEqual(ExecutableNameMatchMode.FilenameOnly, settings.ExecutableNameMatchMode);
            Assert.AreEqual("[]", File.ReadAllText(settingsPath));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void AppListItemSource_ExposesConfiguredTopLevelResultLimit()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update("{\"apps.SearchResultLimit\":\"5\"}");
            using var catalog = new MockAppCatalog();
            using var source = new AppListItemSource(catalog, settings, Microsoft.Extensions.Logging.Abstractions.NullLogger<AppListItemSource>.Instance);

            Assert.AreEqual(5, source.TopLevelResultLimit);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private static string TemporarySettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
    }
}
