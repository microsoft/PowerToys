// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsSettingsTests
{
    [TestMethod]
    [DataRow("*updater*", "", "Contoso UPDATER", @"C:\Apps\Contoso.exe", true)]
    [DataRow("Contoso", "", "Contoso Updater", @"C:\Apps\Contoso.exe", false)]
    [DataRow(" App ? ", "", "App 1", @"C:\Apps\App.exe", true)]
    [DataRow("App ?", "", "App 10", @"C:\Apps\App.exe", false)]
    [DataRow("[App]", "", "[App]", @"C:\Apps\App.exe", true)]
    [DataRow("", @"c:\tools\*", "Editor", @"C:\Tools\Nested\Editor.exe", true)]
    [DataRow("", @"C:\Tools\*", "Editor", @"C:\Toolshed\Editor.exe", false)]
    [DataRow("", "C:/Tools/*.exe", "Editor", @"C:\Tools\Editor.exe", true)]
    [DataRow("", @"\\server\share\*", "Editor", @"\\SERVER\Share\Editor.exe", true)]
    [DataRow("No match", @"C:\Tools\*", "Editor", @"C:\Tools\Editor.exe", true)]
    [DataRow("*Editor*", @"C:\Other\*", "Editor", @"C:\Tools\Editor.exe", true)]
    [DataRow(" ", " ", "Editor", @"C:\Tools\Editor.exe", false)]
    public void ExclusionPatterns_MatchNamesOrPathsWithSimpleWildcards(
        string namePattern, string pathPattern, string name, string path, bool expected)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update(new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create(namePattern)),
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(pathPattern)),
            }.ToJsonString());
            using var visibility = new SettingsAppVisibilityStore(settings);
            var item = new AppCatalogItem(
                "win32:app",
                0,
                new AppCatalogSourceReference("test", path),
                [],
                new Win32AppPayload { Name = name, FullPath = path });

            Assert.AreEqual(expected ? AppVisibility.HiddenByPattern : AppVisibility.Visible, visibility.GetVisibility(item));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void ExclusionPatterns_FormSubmissionPersistsAndIgnoresUnrelatedSettingsChanges()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);
            var changedCount = 0;
            visibility.Changed += (_, _) => changedCount++;
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            var values = new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create("*Updater*")).ToJsonString(),
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(@"C:\Tools\*")).ToJsonString(),
            };
            form.SubmitForm(values.ToJsonString(), string.Empty);

            Assert.AreEqual(1, changedCount);
            var reloaded = new AllAppsSettings(settingsPath);
            Assert.AreEqual("*Updater*", reloaded.ExcludedAppNames.Single());
            Assert.AreEqual(@"C:\Tools\*", reloaded.ExcludedAppPaths.Single());

            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            Assert.AreEqual(1, changedCount);
            visibility.Dispose();
            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[]\"}", string.Empty);
            Assert.AreEqual(1, changedCount);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void PathExclusions_CoverShortcutsAliasesPackagesAndMergedRepresentations()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update(new JsonObject
            {
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(@"C:\Hidden\*")),
            }.ToJsonString());
            using var visibility = new SettingsAppVisibilityStore(settings);
            IAppCatalogPayload[] payloads =
            [
                new Win32AppPayload { LnkFilePath = @"C:\Hidden\App.lnk" },
                new Win32AppPayload { AppExecutionAliasTargetPath = @"C:\Hidden\App.exe" },
                new PackagedAppSnapshot { PackageLocation = @"C:\Hidden\Package" },
            ];
            foreach (var payload in payloads)
            {
                var item = new AppCatalogItem("app", 0, new AppCatalogSourceReference("test", "app"), [], payload);
                Assert.AreEqual(AppVisibility.HiddenByPattern, visibility.GetVisibility(item));
            }

            var preferred = new AppCatalogItem("app", 0, new AppCatalogSourceReference("test", "app"), [], new PackagedAppSnapshot());
            var shortcut = new AppCatalogItem("app", 1, new AppCatalogSourceReference("shortcuts", @"C:\Hidden\App.lnk"), [], new Win32AppPayload());
            var target = new AppCatalogItem("app", 1, new AppCatalogSourceReference("other", "app"), [@"C:\Hidden\App.exe"], new Win32AppPayload());

            Assert.AreEqual(AppVisibility.Visible, visibility.GetVisibility(preferred));
            Assert.AreEqual(AppVisibility.HiddenByPattern, visibility.GetVisibility(preferred.MergeProvenance(shortcut)));
            Assert.AreEqual(AppVisibility.HiddenByPattern, visibility.GetVisibility(preferred.MergeProvenance(target)));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void SettingsVisibilityStore_PersistsHiddenIdentityAcrossSettingsReload()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            var item = CreateCatalogItem("win32:stable-identity");
            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);

            Assert.IsTrue(visibility.SetHidden(item, hidden: true));
            visibility.Persist();
            StringAssert.Contains(File.ReadAllText(settingsPath), "\"futureSetting\": \"preserved\"");
            var temporaryFiles = Directory.GetFiles(
                Path.GetDirectoryName(settingsPath)!,
                $"{Path.GetFileName(settingsPath)}.*.tmp");
            Assert.AreEqual(0, temporaryFiles.Length);

            var reloadedSettings = new AllAppsSettings(settingsPath);
            using var reloadedVisibility = new SettingsAppVisibilityStore(reloadedSettings);
            Assert.AreEqual(AppVisibility.Hidden, reloadedVisibility.GetVisibility(item));

            Assert.IsTrue(reloadedVisibility.SetHidden(item, hidden: false));
            reloadedVisibility.Persist();

            var unhiddenSettings = new AllAppsSettings(settingsPath);
            using var unhiddenVisibility = new SettingsAppVisibilityStore(unhiddenSettings);
            Assert.AreEqual(AppVisibility.Visible, unhiddenVisibility.GetVisibility(item));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SettingsVisibilityStore_RetainsAndClearsHiddenIdentitiesAfterCoalescing(bool preferPackaged)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            const string oldIdentity = @"win32:C:\Tools\app.exe|args:|cwd:C:\Tools";
            const string canonicalIdentity = @"win32:C:\Tools\app.exe|args:";
            var oldItem = CreateCatalogItem(oldIdentity);
            var item = oldItem.WithIdentity(canonicalIdentity);
            if (preferPackaged)
            {
                var packaged = new AppCatalogItem(
                    canonicalIdentity,
                    -1,
                    new AppCatalogSourceReference("packaged", "Contoso.App!app"),
                    [],
                    new PackagedAppSnapshot { Name = "App", UserModelId = "Contoso.App!app" });
                item = item.MergeProvenance(packaged).WithIdentity("packaged:Contoso.App!app");
            }

            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);
            Assert.IsTrue(visibility.SetHidden(oldItem, hidden: true));
            visibility.Persist();

            var reloaded = new AllAppsSettings(settingsPath);
            using var reloadedVisibility = new SettingsAppVisibilityStore(reloaded);
            Assert.AreEqual(AppVisibility.Hidden, reloadedVisibility.GetVisibility(item));
            Assert.IsTrue(reloaded.SetAppHidden(item.Identity, hidden: true));
            Assert.IsTrue(reloadedVisibility.SetHidden(item, hidden: false));
            Assert.AreEqual(AppVisibility.Visible, reloadedVisibility.GetVisibility(item));
            Assert.IsFalse(reloaded.IsAppHidden(oldIdentity));
            reloadedVisibility.Persist();

            using var unhiddenVisibility = new SettingsAppVisibilityStore(new AllAppsSettings(settingsPath));
            Assert.AreEqual(AppVisibility.Visible, unhiddenVisibility.GetVisibility(oldItem));
            Assert.AreEqual(AppVisibility.Visible, unhiddenVisibility.GetVisibility(item));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_AcceptsLegacyBareHiddenIdentity()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[\"win32:legacy\"],\"apps.HideAppDescriptions\":\"true\"}");

            var settings = new AllAppsSettings(settingsPath);
            var item = CreateCatalogItem("win32:legacy");

            using var visibility = new SettingsAppVisibilityStore(settings);
            Assert.AreEqual(AppVisibility.Hidden, visibility.GetVisibility(item));
            Assert.IsTrue(settings.HideAppDescriptions);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_ReplacesThePublishedHiddenIdentitySet()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[\"win32:first\"],\"apps.HideAppDescriptions\":\"true\"}");
            var settings = new AllAppsSettings(settingsPath);

            Assert.IsTrue(settings.IsAppHidden("win32:first"));

            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[\"win32:second\"],\"apps.HideAppDescriptions\":\"false\"}");
            settings.LoadSettings();

            Assert.IsFalse(settings.IsAppHidden("win32:first"));
            Assert.IsTrue(settings.IsAppHidden("win32:second"));
            StringAssert.Contains(File.ReadAllText(settingsPath), "win32:second");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_ReportsDiagnosticsThroughInjectedLogger()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "[]");
            var logger = new RecordingLogger<AllAppsSettings>();

            _ = new AllAppsSettings(settingsPath, logger);

            Assert.AreEqual(LogLevel.Warning, logger.LastLevel);
            Assert.AreEqual(1, logger.LastEventId?.Id);
            StringAssert.Contains(logger.LastMessage, "Failed to parse the Apps settings file");
        }
        finally
        {
            File.Delete(settingsPath);
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
            using var source = new AppListItemSource(catalog, settings);

            Assert.AreEqual(5, source.TopLevelResultLimit);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    private static string TemporarySettingsPath()
        => Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");

    private static AppCatalogItem CreateCatalogItem(string identity)
    {
        var program = TestDataHelper.CreateTestWin32Program("Hidden app");
        return new AppCatalogItem(
            identity,
            priority: 0,
            new AppCatalogSourceReference("test", program.FullPath),
            [],
            Win32AppPayload.From(program));
    }

    private sealed class RecordingLogger<T> : MEL.ILogger<T>
    {
        public LogLevel? LastLevel { get; private set; }

        public EventId? LastEventId { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            LastLevel = logLevel;
            LastEventId = eventId;
            LastMessage = formatter(state, exception);
        }
    }
}
