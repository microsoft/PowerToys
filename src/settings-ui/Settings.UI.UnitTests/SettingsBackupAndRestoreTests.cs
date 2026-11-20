// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
[DoNotParallelize]
public class SettingsBackupAndRestoreTests
{
    private string originalDirectory;
    private string testDirectory;
    private string powerToysPath;
    private string commandPalettePath;
    private string backupPath;
    private SettingsBackupAndRestoreUtils backupUtils;

    [TestInitialize]
    public void Initialize()
    {
        originalDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        testDirectory = Directory.CreateTempSubdirectory("PowerToys-SettingsBackup-").FullName;
        powerToysPath = Path.Combine(testDirectory, "PowerToys");
        commandPalettePath = Path.Combine(testDirectory, "LocalState");
        backupPath = Path.Combine(testDirectory, "Backups");
        Directory.CreateDirectory(powerToysPath);
        Directory.CreateDirectory(commandPalettePath);
        backupUtils = new SettingsBackupAndRestoreUtils(commandPalettePath);
        WriteSettings(powerToysPath, "settings.json", "{\"enabled\":true,\"powertoys_version\":\"test\"}");
        WriteSettings(commandPalettePath, "settings.json", "{\"theme\":\"dark\"}");
        WriteSettings(commandPalettePath, "Microsoft.CmdPal.settings.json", "{\"includeDesktop\":true}");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.CurrentDirectory = originalDirectory;
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void BackupIncludesCommandPaletteAndExtensionSettings()
    {
        WriteSettings(commandPalettePath, "cache.json", "{\"cached\":true}");
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);

        using var archive = OpenLatestBackup();
        Assert.IsNotNull(archive.GetEntry("settings.json"));
        Assert.IsNotNull(archive.GetEntry("CommandPalette/settings.json"));
        Assert.IsNotNull(archive.GetEntry("CommandPalette/Microsoft.CmdPal.settings.json"));
        Assert.IsNull(archive.GetEntry("CommandPalette/cache.json"));
        var manifest = ReadEntry(archive, "manifest.json");
        Assert.IsTrue(manifest["UpdatedFiles"].AsArray().Any(file => file.GetValue<string>() == "\\CommandPalette\\Microsoft.CmdPal.settings.json"));
        Assert.IsNull(ReadEntry(archive, "settings.json")["powertoys_version"]);
    }

    [TestMethod]
    [DataRow("settings.json")]
    [DataRow("Microsoft.CmdPal.settings.json")]
    public void CommandPaletteOnlyChangeRequiresANewBackup(string fileName)
    {
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        Assert.AreEqual("General_SettingsBackupAndRestore_NothingToBackup", backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: true).Message);

        WriteSettings(commandPalettePath, fileName, "{\"changed\":true}");
        var dryRun = backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: true);
        Assert.IsTrue(dryRun.Success);
        Assert.AreEqual(1, Directory.GetFiles(backupPath, "*.ptb").Length);
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        Assert.AreEqual(2, Directory.GetFiles(backupPath, "*.ptb").Length);
        using var archive = OpenLatestBackup();
        Assert.IsTrue(ReadEntry(archive, "CommandPalette/" + fileName)["changed"].GetValue<bool>());
    }

    [TestMethod]
    public void PowerToysChangeKeepsUnchangedCommandPaletteSettingsInBackup()
    {
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        WriteSettings(powerToysPath, "settings.json", "{\"enabled\":false}");
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);

        using var archive = OpenLatestBackup();
        Assert.AreEqual("dark", ReadEntry(archive, "CommandPalette/settings.json")["theme"].GetValue<string>());
        Assert.IsTrue(ReadEntry(archive, "CommandPalette/Microsoft.CmdPal.settings.json")["includeDesktop"].GetValue<bool>());
    }

    [TestMethod]
    public void RestoreMergesIntoExistingCommandPaletteSettings()
    {
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        WriteSettings(commandPalettePath, "settings.json", "{\"theme\":\"light\",\"newSetting\":42}");
        WriteSettings(commandPalettePath, "Microsoft.CmdPal.settings.json", "{\"includeDesktop\":false}");

        Assert.IsTrue(backupUtils.RestoreSettings(powerToysPath, backupPath).Success);
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(commandPalettePath, "settings.json")));
        Assert.AreEqual("dark", settings["theme"].GetValue<string>());
        Assert.AreEqual(42, settings["newSetting"].GetValue<int>());
        var extension = JsonNode.Parse(File.ReadAllText(Path.Combine(commandPalettePath, "Microsoft.CmdPal.settings.json")));
        Assert.IsTrue(extension["includeDesktop"].GetValue<bool>());
        Assert.IsFalse(Directory.Exists(Path.Combine(powerToysPath, "CommandPalette")));
    }

    [TestMethod]
    public void RestoreOfUnchangedSettingsDoesNotRequireRestart()
    {
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        Assert.AreEqual("General_SettingsBackupAndRestore_NothingToRestore", backupUtils.RestoreSettings(powerToysPath, backupPath).Message);
    }

    [TestMethod]
    public void RestoreCreatesMissingCommandPaletteSettingsInPackageDirectory()
    {
        WriteSettings(commandPalettePath, Path.Combine("Extensions", "sample.settings.json"), "{\"option\":42}");
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        Directory.Delete(commandPalettePath, recursive: true);

        Assert.IsTrue(backupUtils.RestoreSettings(powerToysPath, backupPath).Success);
        Assert.IsTrue(File.Exists(Path.Combine(commandPalettePath, "settings.json")));
        Assert.IsTrue(File.Exists(Path.Combine(commandPalettePath, "Microsoft.CmdPal.settings.json")));
        Assert.IsTrue(File.Exists(Path.Combine(commandPalettePath, "Extensions", "sample.settings.json")));
        Assert.IsFalse(Directory.Exists(Path.Combine(powerToysPath, "CommandPalette")));
    }

    [TestMethod]
    public void OlderBackupRestoresWithoutCreatingCommandPaletteSettings()
    {
        Directory.Delete(commandPalettePath, recursive: true);
        Assert.IsTrue(backupUtils.BackupSettings(powerToysPath, backupPath, dryRun: false).Success);
        WriteSettings(powerToysPath, "settings.json", "{\"enabled\":false,\"powertoys_version\":\"current\"}");

        Assert.IsTrue(backupUtils.RestoreSettings(powerToysPath, backupPath).Success);
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(powerToysPath, "settings.json")));
        Assert.IsTrue(settings["enabled"].GetValue<bool>());
        Assert.AreEqual("current", settings["powertoys_version"].GetValue<string>());
        Assert.IsFalse(Directory.Exists(commandPalettePath));
    }

    private static void WriteSettings(string root, string relativePath, string json)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
    }

    private static JsonNode ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name).Open());
        return JsonNode.Parse(reader.ReadToEnd());
    }

    private ZipArchive OpenLatestBackup()
    {
        var path = Directory.GetFiles(backupPath, "*.ptb").OrderByDescending(Path.GetFileName).First();
        return ZipFile.OpenRead(path);
    }
}
