// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class Win32AppPayloadTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void EntriesWithoutShellDisplayNameUseRawName(string displayName)
    {
        var shortcut = TestDataHelper.CreateTestWin32Metadata("dfrgui");
        shortcut.LnkFilePath = @"C:\Links\dfrgui.lnk";
        var executable = TestDataHelper.CreateTestWin32Metadata("dfrgui", @"C:\Tools\dfrgui.exe");
        var script = TestDataHelper.CreateTestWin32Metadata("maintenance", @"C:\Tools\maintenance.cmd");
        var folder = TestDataHelper.CreateTestWin32Metadata("Maintenance", @"C:\Tools\Maintenance");
        folder.AppType = Win32AppType.Folder;
        foreach (var program in new[] { shortcut, executable, script, folder })
        {
            program.DisplayName = displayName;
            var payload = Win32AppPayload.From(program);
            var launchPath = string.IsNullOrEmpty(program.LnkFilePath) ? program.TargetPath : program.LnkFilePath;

            Assert.AreEqual(program.Name, payload.ToAppItem().Name);
            Assert.AreEqual(program.Name, payload.Name);
            Assert.AreEqual(AppCommand.GenerateId(program.Name, program.Description, launchPath), payload.GetCommandId());
        }
    }

    [TestMethod]
    [DataRow(@"C:\Tools\dfrgui.exe", (int)Win32AppType.Win32Application, "dfrgui", "Defragment and Optimize Drives")]
    [DataRow(@"C:\Tools\dfrgui.exe", (int)Win32AppType.Win32Application, "dfrgui", "dfrgui.exe")]
    [DataRow(@"C:\Tools\maintenance.cmd", (int)Win32AppType.Win32Application, "maintenance", "Nightly maintenance")]
    [DataRow(@"C:\Tools\Maintenance", (int)Win32AppType.Folder, "Maintenance", "Administrative tools")]
    public async Task ShellDisplayNamesPreserveCatalogIdentityAndRawNameSearch(
        string path,
        int appType,
        string rawName,
        string displayName)
    {
        var program = TestDataHelper.CreateTestWin32Metadata(rawName, path);
        program.AppType = (Win32AppType)appType;
        using var source = new Win32AppSource(
            new TestProgramSource(path),
            (_, _) => program,
            createWatchers: false);
        var before = (await source.LoadAsync(CancellationToken.None)).Single();
        var canonicalId = new AppCommand(before.ToAppItem()).Id;
        var releasedId = before.Payload.GetCommandId();
        program.DisplayName = displayName;

        var after = (await source.LoadAsync(CancellationToken.None)).Single();
        var payload = (Win32AppPayload)after.Payload;
        var app = after.ToAppItem();
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);
        var matcher = new PrecomputedFuzzyMatcher();

        Assert.AreEqual(rawName, payload.Name);
        Assert.AreEqual(displayName, payload.DisplayName);
        Assert.AreEqual(displayName, row.Title);
        Assert.AreEqual(path, app.LaunchTarget);
        Assert.AreEqual(before.Identity, after.Identity);
        Assert.AreEqual(canonicalId, new AppCommand(app).Id);
        Assert.AreEqual(AppCommand.GenerateId(rawName, program.Description, path), payload.GetCommandId());
        Assert.AreEqual(releasedId, payload.GetCommandId());
        CollectionAssert.AreEqual(before.CommandIds.ToArray(), after.CommandIds.ToArray());
        Assert.AreSame(row, snapshot.GetVisibleApp(canonicalId));
        Assert.AreSame(row, snapshot.GetVisibleApp(releasedId));
        Assert.IsTrue(new AppSearch(displayName, matcher, ExecutableNameMatchMode.Disabled).Evaluate(row).IsExactTitleMatch);
        Assert.IsTrue(new AppSearch(rawName, matcher, ExecutableNameMatchMode.Disabled).Evaluate(row).IsExactMetadataMatch);
    }

    [TestMethod]
    public void UninstallConfirmationUsesDisplayedShortcutTitle()
    {
        var program = TestDataHelper.CreateTestWin32Metadata("raw-shortcut", @"C:\Tools\app.exe");
        program.LnkFilePath = @"C:\Links\raw-shortcut.lnk";
        program.DisplayName = "Friendly shortcut title";
        var app = Win32AppPayload.From(program).ToAppItem();
        var command = app.Commands.OfType<CommandContextItem>()
            .Select(item => item.Command)
            .OfType<UninstallApplicationConfirmation>()
            .Single();

        var result = command.Invoke();

        Assert.IsInstanceOfType<ConfirmationArgs>(result.Args);
        StringAssert.Contains(((ConfirmationArgs)result.Args).Title, app.Name);
    }

    [TestMethod]
    [DataRow("*DEFRAGMENT*", true)]
    [DataRow("dfrgui", true)]
    [DataRow("Description only", false)]
    [DataRow("unrelated", false)]
    public void NameExclusionsMatchShellAndRawNames(string pattern, bool expectedHidden)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"CmdPal-display-name-{Guid.NewGuid():N}.settings.json");
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            settings.Settings.Update(new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create(pattern)),
            }.ToJsonString());
            var visibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settings.FilePath));
            var item = new AppCatalogItem(
                "win32:app",
                0,
                new AppCatalogSourceReference("start-menu", @"C:\Links\dfrgui.lnk"),
                ["Description only"],
                new Win32AppPayload
                {
                    Name = "dfrgui",
                    DisplayName = "Defragment and Optimize Drives",
                    TargetPath = @"C:\Tools\dfrgui.exe",
                    LnkFilePath = @"C:\Links\dfrgui.lnk",
                    Description = "Description only",
                });

            Assert.AreEqual(expectedHidden ? AppVisibility.HiddenByPattern : AppVisibility.Visible, TestDataHelper.GetVisibility(visibility, item, settings: settings, aliases: settingsAliases));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private sealed class TestProgramSource : IWin32ProgramSource
    {
        private readonly string _path;

        public string Id => "custom:test";

        public int Priority => 0;

        public bool IsEnabled => true;

        public Win32ProgramSourceProfile Profile => Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.IncludeNonApplications;

        public string CacheKey => Id;

        public string ConfigurationKey => CacheKey;

        public IReadOnlyList<string> WatchPaths => [];

        public TestProgramSource(string path)
        {
            _path = path;
        }

        public IEnumerable<string> GetPaths()
        {
            return [_path];
        }

        public bool IsRelevantPath(string path)
        {
            return string.Equals(_path, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
