// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsPageTests : AppsTestBase
{
    [TestMethod]
    public void AllAppsPage_Constructor_ThrowsOnNullAppCache()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new AllAppsPage(null!));
    }

    [TestMethod]
    public void AllAppsPage_WithMockCache_InitializesSuccessfully()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act
        var page = new AllAppsPage(mockCache);

        // Assert
        Assert.IsNotNull(page);
        Assert.IsNotNull(page.Name);
        Assert.IsNotNull(page.Icon);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ReturnsEmptyWithEmptyCache()
    {
        // Act - Wait for initialization to complete
        await WaitForPageInitializationAsync();
        var items = Page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(0, items.Length);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ReturnsAppsFromCacheAsync()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var win32App = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        var uwpApp = TestDataHelper.CreateTestUWPApplication("Calculator");

        mockCache.AddWin32Program(win32App);
        mockCache.AddUWPApplication(uwpApp);

        var page = new AllAppsPage(mockCache);

        // Wait a bit for initialization to complete
        await Task.Delay(100);

        // Act
        var items = page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(2, items.Length);

        // we need to loop the items to ensure we got the correct ones
        Assert.IsTrue(items.Any(i => i.Title == "Notepad"));
        Assert.IsTrue(items.Any(i => i.Title == "Calculator"));
    }

    [TestMethod]
    public async Task AllAppsPage_TryGetCurrentItemUsesPublishedCatalogWithoutReloading()
    {
        var mockCache = new MockAppCache();
        mockCache.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe"));
        var page = new AllAppsPage(mockCache);
        await Task.Delay(100);

        var expected = page.GetItems().OfType<AppListItem>().Single();

        Assert.IsTrue(page.TryGetCurrentItem(expected.Command.Id, out var actual));
        Assert.AreSame(expected, actual);
        Assert.IsFalse(page.TryGetCurrentItem("missing", out var missing));
        Assert.IsNull(missing);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_HidesSubtitlesWhenSettingEnabled()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var win32App = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        mockCache.AddWin32Program(win32App);

        try
        {
            AllAppsSettings.Instance.Settings.Update("{\"apps.HideAppDescriptions\": \"true\"}");

            var page = new AllAppsPage(mockCache);
            await Task.Delay(100);

            // Act
            var items = page.GetItems();

            // Assert
            Assert.AreEqual(1, items.Length);
            var appItem = items.OfType<AppListItem>().Single();
            Assert.AreEqual(string.Empty, appItem.Subtitle);
        }
        finally
        {
            AllAppsSettings.Instance.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");
        }
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ShowsSubtitlesWhenSettingDisabled()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var win32App = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        mockCache.AddWin32Program(win32App);

        try
        {
            AllAppsSettings.Instance.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");

            var page = new AllAppsPage(mockCache);
            await Task.Delay(100);

            // Act
            var items = page.GetItems();

            // Assert
            Assert.AreEqual(1, items.Length);
            var appItem = items.OfType<AppListItem>().Single();
            Assert.IsFalse(string.IsNullOrEmpty(appItem.Subtitle));
        }
        finally
        {
            AllAppsSettings.Instance.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");
        }
    }

    [TestMethod]
    public void AppListItem_DefersWin32RowAndHeroIconsToUiLoader()
    {
        var app = new AppItem
        {
            Name = "Test App",
            IcoPath = "C:\\Windows\\System32\\shell32.dll,1",
            JumboIconPath = "C:\\Windows\\System32\\imageres.dll,2",
            ExePath = "C:\\Program Files\\Example\\app.exe",
        };

        var item = new AppListItem(app, useThumbnails: true);

        var rowIcon = (IconInfo)item.Icon!;
        Assert.IsTrue(AppIconProtocol.TryParse(rowIcon.Light.Icon, out var rowCandidates, out var rowJumbo));
        Assert.IsFalse(rowJumbo);
        CollectionAssert.AreEqual(new[] { app.IcoPath, app.ExePath }, rowCandidates);
        Assert.AreSame(rowIcon, item.Command.Icon);

        var details = (Details)item.Details!;
        var heroIcon = (IconInfo)details.HeroImage;
        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var heroCandidates, out var heroJumbo));
        Assert.IsTrue(heroJumbo);
        CollectionAssert.AreEqual(new[] { app.JumboIconPath, app.IcoPath, app.ExePath }, heroCandidates);
    }

    [TestMethod]
    [DataRow(".lnk")]
    [DataRow(".LNK")]
    public void AppListItem_ShortcutHeroPrefersShellRenderingAndKeepsResourceFallbacks(string extension)
    {
        var app = new AppItem
        {
            Name = "Java",
            IcoPath = @"C:\Program Files\Java\java.exe,0",
            ExePath = @"C:\Start Menu\Java" + extension,
            FullExecutablePath = @"C:\Program Files\Java\javacpl.exe",
        };
        var item = new AppListItem(app, useThumbnails: true);

        var rowIcon = (IconInfo)item.Icon!;
        Assert.IsTrue(AppIconProtocol.TryParse(rowIcon.Light.Icon, out var rowCandidates, out var rowJumbo));
        Assert.IsFalse(rowJumbo);
        CollectionAssert.AreEqual(new[] { app.IcoPath, app.FullExecutablePath }, rowCandidates);

        var heroIcon = (IconInfo)((Details)item.Details!).HeroImage;
        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var heroCandidates, out var heroJumbo));
        Assert.IsTrue(heroJumbo);
        CollectionAssert.AreEqual(new[] { app.ExePath, app.IcoPath, app.FullExecutablePath }, heroCandidates);
    }

    [TestMethod]
    public void AppListItem_ShortcutHeroKeepsExplicitJumboSourceFirst()
    {
        var app = new AppItem
        {
            Name = "Custom App",
            IcoPath = @"C:\Icons\small.ico,0",
            JumboIconPath = @"C:\Icons\large.ico,0",
            ExePath = @"C:\Start Menu\Custom App.lnk",
            FullExecutablePath = @"C:\Apps\custom.exe",
        };
        var item = new AppListItem(app, useThumbnails: true);
        var heroIcon = (IconInfo)((Details)item.Details!).HeroImage;

        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var candidates, out var jumbo));
        Assert.IsTrue(jumbo);
        CollectionAssert.AreEqual(new[] { app.JumboIconPath, app.IcoPath, app.FullExecutablePath }, candidates);
    }

    [TestMethod]
    public void AppListItem_NamespaceShortcutHeroUsesOneShellCandidate()
    {
        var shortcutPath = @"C:\Desktop\About Java - Shortcut.lnk";
        var app = new AppItem
        {
            Name = "About Java",
            IcoPath = shortcutPath,
            ExePath = shortcutPath,
        };
        var item = new AppListItem(app, useThumbnails: true);
        var heroIcon = (IconInfo)((Details)item.Details!).HeroImage;

        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var candidates, out var jumbo));
        Assert.IsTrue(jumbo);
        CollectionAssert.AreEqual(new[] { shortcutPath }, candidates);
    }

    [TestMethod]
    public void AppListItem_KeepsPackagedIconAssetsAsDirectPaths()
    {
        var app = new AppItem
        {
            Name = "Test Packaged App",
            IcoPath = "C:\\Program Files\\WindowsApps\\Example\\small.png",
            JumboIconPath = "C:\\Program Files\\WindowsApps\\Example\\large.png",
            IsPackaged = true,
        };

        var item = new AppListItem(app, useThumbnails: true);

        var rowIcon = (IconInfo)item.Icon!;
        Assert.AreEqual(app.IcoPath, rowIcon.Light.Icon);

        var details = (Details)item.Details!;
        var heroIcon = (IconInfo)details.HeroImage;
        Assert.AreEqual(app.JumboIconPath, heroIcon.Light.Icon);
    }
}
