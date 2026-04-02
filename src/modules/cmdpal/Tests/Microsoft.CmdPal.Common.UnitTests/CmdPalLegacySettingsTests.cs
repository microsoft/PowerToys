// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Common.UnitTests;

[TestClass]
public class CmdPalLegacySettingsTests
{
    private string _tempDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Directory.CreateTempSubdirectory(nameof(CmdPalLegacySettingsTests)).FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public void EnsureLegacySettingsBackup_SettingsFileExists_CreatesBackup()
    {
        var settingsPath = CmdPalLegacySettings.SettingsJsonPath(_tempDir);
        var backupPath = CmdPalLegacySettings.LegacySettingsBackupJsonPath(_tempDir);
        File.WriteAllText(settingsPath, "{\"key\":true}");

        CmdPalLegacySettings.EnsureLegacySettingsBackup(_tempDir);

        Assert.IsTrue(File.Exists(backupPath));
        Assert.AreEqual(File.ReadAllText(settingsPath), File.ReadAllText(backupPath));
    }

    [TestMethod]
    public void LegacySettingsBackupJsonPath_UsesBackupExtension()
    {
        var backupPath = CmdPalLegacySettings.LegacySettingsBackupJsonPath(_tempDir);

        Assert.AreEqual("settings.legacy.bak", Path.GetFileName(backupPath));
    }

    [TestMethod]
    public void EnsureLegacySettingsBackup_BackupExists_DoesNotOverwriteBackup()
    {
        var settingsPath = CmdPalLegacySettings.SettingsJsonPath(_tempDir);
        var backupPath = CmdPalLegacySettings.LegacySettingsBackupJsonPath(_tempDir);
        File.WriteAllText(settingsPath, "{\"key\":true}");
        File.WriteAllText(backupPath, "{\"key\":false}");

        CmdPalLegacySettings.EnsureLegacySettingsBackup(_tempDir);

        Assert.AreEqual("{\"key\":false}", File.ReadAllText(backupPath));
    }

    [TestMethod]
    public void LegacySettingsMigrationSourceJsonPath_BackupExists_ReturnsBackupPath()
    {
        var backupPath = CmdPalLegacySettings.LegacySettingsBackupJsonPath(_tempDir);
        File.WriteAllText(backupPath, "{\"key\":true}");

        var migrationSourcePath = CmdPalLegacySettings.LegacySettingsMigrationSourceJsonPath(_tempDir);

        Assert.AreEqual(backupPath, migrationSourcePath);
    }

    [TestMethod]
    public void LegacySettingsMigrationSourceJsonPath_BackupMissing_ReturnsLiveSettingsPath()
    {
        var settingsPath = CmdPalLegacySettings.SettingsJsonPath(_tempDir);

        var migrationSourcePath = CmdPalLegacySettings.LegacySettingsMigrationSourceJsonPath(_tempDir);

        Assert.AreEqual(settingsPath, migrationSourcePath);
    }
}
