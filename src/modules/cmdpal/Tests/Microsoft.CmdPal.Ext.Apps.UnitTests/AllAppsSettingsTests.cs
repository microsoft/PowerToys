// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsSettingsTests
{
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
            var visibility = new SettingsAppVisibilityStore(settings);
            Assert.IsTrue(visibility.SetHidden(oldItem, hidden: true));
            visibility.Persist();

            var reloaded = new AllAppsSettings(settingsPath);
            var reloadedVisibility = new SettingsAppVisibilityStore(reloaded);
            Assert.IsTrue(reloadedVisibility.IsHidden(item));
            Assert.IsTrue(reloaded.SetAppHidden(item.Identity, hidden: true));
            Assert.IsTrue(reloadedVisibility.SetHidden(item, hidden: false));
            Assert.IsFalse(reloadedVisibility.IsHidden(item));
            Assert.IsFalse(reloaded.IsAppHidden(oldIdentity));
            reloadedVisibility.Persist();

            var unhiddenVisibility = new SettingsAppVisibilityStore(new AllAppsSettings(settingsPath));
            Assert.IsFalse(unhiddenVisibility.IsHidden(oldItem));
            Assert.IsFalse(unhiddenVisibility.IsHidden(item));
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
            var visibility = new SettingsAppVisibilityStore(settings);

            Assert.IsTrue(visibility.SetHidden(item, hidden: true));
            visibility.Persist();
            StringAssert.Contains(File.ReadAllText(settingsPath), "\"futureSetting\": \"preserved\"");
            var temporaryFiles = Directory.GetFiles(
                Path.GetDirectoryName(settingsPath)!,
                $"{Path.GetFileName(settingsPath)}.*.tmp");
            Assert.AreEqual(0, temporaryFiles.Length);

            var reloadedSettings = new AllAppsSettings(settingsPath);
            var reloadedVisibility = new SettingsAppVisibilityStore(reloadedSettings);
            Assert.IsTrue(reloadedVisibility.IsHidden(item));

            Assert.IsTrue(reloadedVisibility.SetHidden(item, hidden: false));
            reloadedVisibility.Persist();

            var unhiddenSettings = new AllAppsSettings(settingsPath);
            Assert.IsFalse(new SettingsAppVisibilityStore(unhiddenSettings).IsHidden(item));
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

            Assert.IsTrue(new SettingsAppVisibilityStore(settings).IsHidden(item));
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
