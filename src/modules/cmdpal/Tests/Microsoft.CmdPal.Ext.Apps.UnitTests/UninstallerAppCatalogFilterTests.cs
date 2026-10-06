// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class UninstallerAppCatalogFilterTests
{
    [TestMethod]
    public void Includes_SettingDisabled_KeepsUninstaller()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            using var filter = new UninstallerAppCatalogFilter(settings);

            Assert.IsTrue(filter.Includes(CreateWin32Item(@"C:\Apps\uninstall.exe")));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [DataTestMethod]
    [DataRow(@"C:\Apps\uninst.exe")]
    [DataRow(@"C:\Apps\unins000.exe")]
    [DataRow(@"C:\Apps\uninst000.exe")]
    [DataRow(@"C:\Apps\uninstall.exe")]
    [DataRow(@"C:\Apps\Uninstall Contoso.exe")]
    [DataRow(@"C:\Apps\Désinstaller Contoso.exe")]
    [DataRow(@"C:\Apps\アンインストール Contoso.exe")]
    public void Includes_SettingEnabled_ExcludesCommonExecutableNames(string executablePath)
    {
        var settingsPath = EnabledSettingsPath();
        try
        {
            using var filter = new UninstallerAppCatalogFilter(new AllAppsSettings(settingsPath));

            Assert.IsFalse(filter.Includes(CreateWin32Item(executablePath)));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void Includes_SettingEnabled_ExcludesLocalizedShortcutName()
    {
        var settingsPath = EnabledSettingsPath();
        try
        {
            using var filter = new UninstallerAppCatalogFilter(new AllAppsSettings(settingsPath));
            var item = CreateWin32Item(
                @"C:\Apps\Contoso.exe",
                @"C:\Start Menu\Odinstaluj Contoso.lnk");

            Assert.IsFalse(filter.Includes(item));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void Includes_SettingEnabled_KeepsNonPrefixAndNonExecutableMatches()
    {
        var settingsPath = EnabledSettingsPath();
        try
        {
            using var filter = new UninstallerAppCatalogFilter(new AllAppsSettings(settingsPath));

            Assert.IsTrue(filter.Includes(CreateWin32Item(@"C:\Apps\ContosoUninstallHelper.exe")));
            Assert.IsTrue(filter.Includes(CreateWin32Item(@"C:\Apps\Uninstall Notes.txt")));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void Includes_SettingEnabled_KeepsPackagedApplication()
    {
        var settingsPath = EnabledSettingsPath();
        try
        {
            using var filter = new UninstallerAppCatalogFilter(new AllAppsSettings(settingsPath));
            var item = new AppCatalogItem(
                "packaged:uninstall",
                priority: 0,
                new AppCatalogSourceReference("packaged", "Contoso_1.0.0.0_x64__publisher"),
                [],
                new PackagedAppSnapshot
                {
                    Name = "Uninstall Contoso",
                    PackageFullName = "Contoso_1.0.0.0_x64__publisher",
                });

            Assert.IsTrue(filter.Includes(item));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void Includes_OverlappingShortcutRepresentation_UsesSourceProvenance()
    {
        var settingsPath = EnabledSettingsPath();
        try
        {
            using var filter = new UninstallerAppCatalogFilter(new AllAppsSettings(settingsPath));
            var item = CreateWin32Item(@"C:\Apps\Contoso.exe");
            var shortcutRepresentation = new AppCatalogItem(
                item.Identity,
                priority: 1,
                new AppCatalogSourceReference("start-menu", @"C:\Start Menu\Uninstall Contoso.lnk"),
                [],
                item.Payload);

            Assert.IsFalse(filter.Includes(item.MergeProvenance(shortcutRepresentation)));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private static AppCatalogItem CreateWin32Item(string executablePath, string? shortcutPath = null)
    {
        var name = Path.GetFileNameWithoutExtension(executablePath);
        var program = TestDataHelper.CreateTestWin32Metadata(name, executablePath);
        program.LnkFilePath = shortcutPath ?? string.Empty;
        return new AppCatalogItem(
            $"win32:{name}",
            priority: 0,
            new AppCatalogSourceReference("test", shortcutPath ?? executablePath),
            [],
            Win32AppPayload.From(program));
    }

    private static string EnabledSettingsPath()
    {
        var path = TemporarySettingsPath();
        File.WriteAllText(path, "{\"apps.HideUninstallers\":\"true\"}");
        return path;
    }

    private static string TemporarySettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
    }
}
