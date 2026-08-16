// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

[TestClass]
public sealed class BackupRestoreCompatibilityTests
{
    private static readonly int[] ExpectedMergedItems = [1, 2, 3, 3];

    private static readonly string[] PreviewArchivePaths =
    [
        "manifest.json",
        @"Keyboard Manager\default.json",
        @"FancyZones\settings.json",
        @"PowerToys\log_settings.json",
    ];

    private static readonly string[] PreviewCurrentPaths =
    [
        @"Keyboard Manager\default.json",
        @"FancyZones\settings.json",
        @"Workspaces\workspaces.json",
        @"Unlisted\diagnostics.json",
    ];

    [TestMethod]
    public void ProductionPolicyPreservesIncludeIgnoreAndCustomOverwrite()
    {
        BackupRestorePolicy policy = LoadProductionPolicy();

        Assert.IsTrue(policy.ShouldInclude(@"Keyboard Manager\default.json"));
        Assert.AreEqual(RestoreMode.Overwrite, policy.GetRestoreMode(@"Keyboard Manager\default.json"));
        Assert.IsTrue(policy.ShouldInclude(@"FancyZones\settings.json"));
        Assert.AreEqual(RestoreMode.Merge, policy.GetRestoreMode(@"FancyZones\settings.json"));
        Assert.IsTrue(policy.IsIgnored(@"PowerToys\log_settings.json"));
        Assert.IsFalse(policy.ShouldInclude(@"PowerToys\log_settings.json"));
        Assert.IsFalse(policy.ShouldInclude(@"PowerToys\diagnostics.log"));
        Assert.IsTrue(policy.RestartAfterRestore);
    }

    [TestMethod]
    public void MergePreservesCurrentOnlyValuesAndAppliesBackupValues()
    {
        const string current = """
            {
              "currentOnly": true,
              "scalar": "current",
              "nested": { "keep": 1, "replace": 1 },
              "items": [1, 2]
            }
            """;
        const string backup = """
            {
              "scalar": "backup",
              "nested": { "replace": 2, "add": 3 },
              "items": [2, 3, 3],
              "backupOnly": true
            }
            """;

        string merged = JsonSettingsMerge.Merge(current, backup);
        using JsonDocument document = JsonDocument.Parse(merged);
        JsonElement root = document.RootElement;

        Assert.IsTrue(root.GetProperty("currentOnly").GetBoolean());
        Assert.AreEqual("backup", root.GetProperty("scalar").GetString());
        Assert.AreEqual(1, root.GetProperty("nested").GetProperty("keep").GetInt32());
        Assert.AreEqual(2, root.GetProperty("nested").GetProperty("replace").GetInt32());
        Assert.AreEqual(3, root.GetProperty("nested").GetProperty("add").GetInt32());
        CollectionAssert.AreEqual(ExpectedMergedItems, root.GetProperty("items").EnumerateArray().Select(item => item.GetInt32()).ToArray());
        Assert.IsTrue(root.GetProperty("backupOnly").GetBoolean());
    }

    [TestMethod]
    public void ExportFilteringPreservesIgnoredSettingsAndPowerToysRunPluginRules()
    {
        BackupRestorePolicy policy = LoadProductionPolicy();
        const string generalSettings = """{"powertoys_version":"1","keep":true}""";
        const string runSettings = """
            {
              "plugins": [
                {
                  "Id": "525995402BEF4A8CA860D92F6D108092",
                  "IconPathDark": "dark.png",
                  "IconPathLight": "light.png",
                  "Keep": true
                }
              ]
            }
            """;

        using JsonDocument general = JsonDocument.Parse(policy.CreateExportVersion(@"\settings.json", generalSettings));
        Assert.IsFalse(general.RootElement.TryGetProperty("powertoys_version", out _));
        Assert.IsTrue(general.RootElement.GetProperty("keep").GetBoolean());

        using JsonDocument run = JsonDocument.Parse(policy.CreateExportVersion(@"\PowerToys Run\settings.json", runSettings));
        JsonElement plugin = run.RootElement.GetProperty("plugins")[0];
        Assert.IsFalse(plugin.TryGetProperty("IconPathDark", out _));
        Assert.IsFalse(plugin.TryGetProperty("IconPathLight", out _));
        Assert.IsTrue(plugin.GetProperty("Keep").GetBoolean());

        using JsonDocument archiveNormalizedRun = JsonDocument.Parse(policy.CreateExportVersion(@"PowerToys Run\settings.json", runSettings));
        JsonElement archiveNormalizedPlugin = archiveNormalizedRun.RootElement.GetProperty("plugins")[0];
        Assert.IsFalse(archiveNormalizedPlugin.TryGetProperty("IconPathDark", out _));
        Assert.IsFalse(archiveNormalizedPlugin.TryGetProperty("IconPathLight", out _));
    }

    [TestMethod]
    public void RestorePreviewListsModulesExclusionsModesAndRestart()
    {
        BackupRestorePolicy policy = LoadProductionPolicy();
        RestorePreviewViewModel preview = RestorePreviewViewModel.Create(
            policy,
            PreviewArchivePaths,
            PreviewCurrentPaths);

        RestorePreviewItem keyboardManager = preview.Items.Single(item => item.Module == "Keyboard Manager");
        RestorePreviewItem fancyZones = preview.Items.Single(item => item.Module == "FancyZones");
        RestorePreviewItem ignored = preview.Items.Single(item => item.SettingsPath == @"PowerToys\log_settings.json");

        Assert.IsTrue(keyboardManager.Included);
        Assert.AreEqual(RestoreMode.Overwrite, keyboardManager.RestoreMode);
        Assert.IsTrue(fancyZones.Included);
        Assert.AreEqual(RestoreMode.Merge, fancyZones.RestoreMode);
        Assert.IsFalse(ignored.Included);
        Assert.AreEqual("Excluded by IgnoreFiles", ignored.ExclusionReason);
        Assert.IsFalse(preview.Items.Any(item => item.SettingsPath == @"Workspaces\workspaces.json"));
        Assert.IsFalse(preview.Items.Any(item => item.SettingsPath == @"Unlisted\diagnostics.json"));
        Assert.IsTrue(preview.RestartAfterRestore);
        StringAssert.Contains(preview.SecurityBoundaryStatement, "handle-relative I/O remain the security boundary");
    }

    [TestMethod]
    public void RestorePreviewRetainsWindowsDistinctUnicodeArchivePaths()
    {
        const string policyJson = """
            {
              "IncludeFiles": ["*"],
              "IgnoreFiles": [],
              "CustomRestoreSettings": {},
              "RestartAfterRestore": false
            }
            """;
        BackupRestorePolicy policy = BackupRestorePolicy.Parse(policyJson);

        RestorePreviewViewModel preview = RestorePreviewViewModel.Create(
            policy,
            ["σ.json", "ς.json"],
            []);

        Assert.AreEqual(2, preview.Items.Count);
        Assert.IsTrue(preview.Items.Any(item => item.SettingsPath == "σ.json"));
        Assert.IsTrue(preview.Items.Any(item => item.SettingsPath == "ς.json"));
    }

    [TestMethod]
    public void CustomOverwriteKeepsWindowsDistinctUnicodePathsSeparate()
    {
        const string policyJson = """
            {
              "IncludeFiles": ["*"],
              "IgnoreFiles": [],
              "CustomRestoreSettings": {
                "\\σ.json": { "overwrite": true }
              }
            }
            """;
        BackupRestorePolicy policy = BackupRestorePolicy.Parse(policyJson);

        Assert.AreEqual(RestoreMode.Overwrite, policy.GetRestoreMode("σ.json"));
        Assert.AreEqual(RestoreMode.Merge, policy.GetRestoreMode("ς.json"));
    }

    [TestMethod]
    public void RestorePreviewDistinguishesCreateFromExistingTargetActions()
    {
        const string policyJson = """
            {
              "IncludeFiles": ["*"],
              "IgnoreFiles": [],
              "CustomRestoreSettings": {
                "\\existing.json": { "overwrite": true },
                "\\σ.json": { "overwrite": true }
              }
            }
            """;
        BackupRestorePolicy policy = BackupRestorePolicy.Parse(policyJson);

        RestorePreviewViewModel preview = RestorePreviewViewModel.Create(
            policy,
            ["existing.json", "new.json", "σ.json", "ς.json"],
            ["existing.json", "σ.json"]);

        Assert.AreEqual(RestoreMode.Overwrite, preview.Items.Single(item => item.SettingsPath == "existing.json").RestoreMode);
        Assert.AreEqual(RestoreMode.Create, preview.Items.Single(item => item.SettingsPath == "new.json").RestoreMode);
        Assert.AreEqual(RestoreMode.Overwrite, preview.Items.Single(item => item.SettingsPath == "σ.json").RestoreMode);
        Assert.AreEqual(RestoreMode.Create, preview.Items.Single(item => item.SettingsPath == "ς.json").RestoreMode);
    }

    private static BackupRestorePolicy LoadProductionPolicy()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "backup_restore_settings.json");
        return BackupRestorePolicy.Parse(File.ReadAllText(path));
    }
}
