// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

[TestClass]
public sealed class SettingsBackupRestoreEngineTests
{
    private static readonly string[] ExpectedArchiveEntries = ["settings.json", "FancyZones/settings.json", "manifest.json"];

    private const string PolicyJson = """
        {
          "IncludeFiles": ["*.json"],
          "IgnoreFiles": ["*\\ignored.json"],
          "IgnoredSettings": {
            "settings.json": ["machineOnly"]
          },
          "CustomRestoreSettings": {
            "\\Keyboard Manager\\default.json": { "overwrite": true }
          },
          "RestartAfterRestore": true
        }
        """;

    [TestMethod]
    public void ProductionBackupWritesLegacyArchiveAndDryRunDoesNotWrite()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"settings\settings.json", """{"machineOnly":"x","value":1}""");
        test.CreateFile(@"settings\FancyZones\settings.json", """{"zone":1}""");
        test.CreateFile(@"settings\PowerToys\ignored.json", """{"ignored":true}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = new(2026, 8, 16, 7, 0, 0, DateTimeKind.Utc);

        BackupOperationResult first = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);
        BackupOperationResult unchanged = engine.Backup(settings, backups, staging, true, "1.2.3", "test-machine", now.AddMinutes(1));

        Assert.IsTrue(first.BackupCreated);
        Assert.IsFalse(first.PreviousBackupExists);
        Assert.IsFalse(unchanged.BackupCreated);
        Assert.IsTrue(unchanged.PreviousBackupExists);
        Assert.AreEqual(1, Directory.GetFiles(backups, "*.ptb").Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);

        using ZipArchive archive = ZipFile.OpenRead(Path.Combine(backups, first.ArchiveFileName!));
        CollectionAssert.AreEquivalent(
            ExpectedArchiveEntries,
            archive.Entries.Select(entry => entry.FullName).ToArray());
        using StreamReader settingsReader = new(archive.GetEntry("settings.json")!.Open());
        using JsonDocument exported = JsonDocument.Parse(settingsReader.ReadToEnd());
        Assert.IsFalse(exported.RootElement.TryGetProperty("machineOnly", out _));
    }

    [TestMethod]
    public void ProductionBackupManifestAndChangedDryRunPreserveCompatibility()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"settings\settings.json", """{"value":1}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = new(2026, 8, 16, 7, 0, 0, DateTimeKind.Utc);
        engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);
        File.WriteAllText(Path.Combine(settings, "settings.json"), """{"value":2}""");

        BackupOperationResult dryRun = engine.Backup(settings, backups, staging, true, "1.2.3", "test-machine", now.AddMinutes(1));
        JsonNode? manifest = engine.GetLatestManifest(backups, staging);

        Assert.IsTrue(dryRun.BackupCreated);
        Assert.AreEqual(1, Directory.GetFiles(backups, "*.ptb").Length);
        Assert.AreEqual("1.2.3", manifest!["Version"]!.GetValue<string>());
        Assert.AreEqual("test-machine", manifest["BackupSource"]!.GetValue<string>());
        Assert.AreEqual(@"\settings.json", manifest["UpdatedFiles"]![0]!.GetValue<string>());
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void ProductionRestoreUsesCreateMergeOverwriteIgnoreAndRestart()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", """{"backup":true,"shared":"backup"}""");
        test.CreateFile(@"source\FancyZones\settings.json", """{"fromBackup":true,"shared":"backup"}""");
        test.CreateFile(@"source\Keyboard Manager\default.json", """{"overwrite":"backup"}""");
        test.CreateFile(@"source\NewModule\settings.json", """{"created":true}""");
        test.CreateFile(@"source\PowerToys\ignored.json", """{"ignored":"backup"}""");
        test.CreateFile(@"target\settings.json", """{"current":true,"shared":"current"}""");
        test.CreateFile(@"target\FancyZones\settings.json", """{"currentOnly":true,"shared":"current"}""");
        test.CreateFile(@"target\Keyboard Manager\default.json", """{"overwrite":"current","keep":true}""");
        test.CreateFile(@"target\PowerToys\ignored.json", """{"ignored":"current"}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        Assert.IsTrue(result.RestartRequired);
        using JsonDocument merged = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, @"FancyZones\settings.json")));
        Assert.IsTrue(merged.RootElement.GetProperty("currentOnly").GetBoolean());
        Assert.IsTrue(merged.RootElement.GetProperty("fromBackup").GetBoolean());
        Assert.AreEqual("backup", merged.RootElement.GetProperty("shared").GetString());
        using JsonDocument overwritten = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, @"Keyboard Manager\default.json")));
        Assert.IsFalse(overwritten.RootElement.TryGetProperty("keep", out _));
        Assert.AreEqual("backup", overwritten.RootElement.GetProperty("overwrite").GetString());
        Assert.IsTrue(File.Exists(Path.Combine(target, @"NewModule\settings.json")));
        Assert.AreEqual("""{"ignored":"current"}""", File.ReadAllText(Path.Combine(target, @"PowerToys\ignored.json")));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void ProductionPreviewListsActionsExclusionsAndRestart()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\FancyZones\settings.json", "{}");
        test.CreateFile(@"source\Keyboard Manager\default.json", "{}");
        test.CreateFile(@"source\NewModule\settings.json", "{}");
        test.CreateFile(@"source\PowerToys\ignored.json", "{}");
        test.CreateFile(@"target\FancyZones\settings.json", "{}");
        test.CreateFile(@"target\Keyboard Manager\default.json", "{}");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        BackupOperationResult backup = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        using (ZipArchive archive = ZipFile.Open(Path.Combine(backups, backup.ArchiveFileName!), ZipArchiveMode.Update))
        using (StreamWriter writer = new(archive.CreateEntry("PowerToys/ignored.json").Open()))
        {
            writer.Write("{}");
        }

        RestorePreviewViewModel preview = engine.CreateRestorePreview(target, backups, staging);

        Assert.AreEqual(RestoreMode.Merge, preview.Items.Single(item => item.Module == "FancyZones").RestoreMode);
        Assert.AreEqual(RestoreMode.Overwrite, preview.Items.Single(item => item.Module == "Keyboard Manager").RestoreMode);
        Assert.AreEqual(RestoreMode.Create, preview.Items.Single(item => item.Module == "NewModule").RestoreMode);
        Assert.IsFalse(preview.Items.Single(item => item.SettingsPath.EndsWith("ignored.json", StringComparison.Ordinal)).Included);
        Assert.IsTrue(preview.RestartAfterRestore);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void RestoreRejectsArchiveChangedAfterPreview()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", """{"value":1}""");
        test.CreateFile(@"target\settings.json", """{"value":0}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        RestorePreviewViewModel preview = engine.CreateRestorePreview(target, backups, staging);
        using (ZipArchive archive = ZipFile.Open(Path.Combine(backups, preview.ArchiveFileName!), ZipArchiveMode.Update))
        {
            archive.CreateEntry("changed.json");
        }

        Assert.ThrowsException<InvalidDataException>(() =>
            engine.Restore(target, backups, staging, preview.ArchiveFileName, preview.ArchiveSha256));
        Assert.AreEqual("""{"value":0}""", File.ReadAllText(Path.Combine(target, "settings.json")));
    }

    [TestMethod]
    public void ProductionEnginePreservesWindowsDistinctUnicodePaths()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\σ.json", """{"name":"sigma"}""");
        test.CreateFile(@"source\ς.json", """{"name":"final-sigma"}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);

        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        using JsonDocument sigma = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "σ.json")));
        using JsonDocument finalSigma = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "ς.json")));
        Assert.AreEqual("sigma", sigma.RootElement.GetProperty("name").GetString());
        Assert.AreEqual("final-sigma", finalSigma.RootElement.GetProperty("name").GetString());
    }

    [TestMethod]
    public void UnrelatedLockedFilesAreNotOpenedDuringFiltering()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string unrelatedTarget = test.CreateDirectory("unrelated-target");
        test.CreateFile(@"source\settings.json", """{"value":1}""");
        string sourceUnrelated = test.CreateFile(@"source\unrelated.bin", "not settings");
        string backupUnrelated = test.CreateFile(@"backups\unrelated.bin", "not an archive");
        test.CreateDirectoryJunction(@"backups\unrelated-junction", unrelatedTarget);
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        using FileStream sourceLock = new(sourceUnrelated, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using FileStream backupLock = new(backupUnrelated, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        BackupOperationResult backup = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        RestorePreviewViewModel preview = engine.CreateRestorePreview(target, backups, staging);

        Assert.IsTrue(backup.BackupCreated);
        Assert.AreEqual(1, preview.Items.Count);
        Assert.AreEqual("settings.json", preview.Items[0].SettingsPath);
    }

    [TestMethod]
    public void UnsafeArchiveFailsClosedBeforePreviewOrRestoreWrites()
    {
        using TestDirectory test = new();
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string targetSettings = test.CreateFile(@"target\settings.json", """{"value":"current"}""");
        string archivePath = Path.Combine(backups, $"settings_{DateTime.UtcNow.ToFileTimeUtc()}.ptb");
        using (FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        using (StreamWriter writer = new(archive.CreateEntry("../settings.json").Open()))
        {
            writer.Write("""{"value":"untrusted"}""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);

        Assert.ThrowsException<InvalidDataException>(() => engine.CreateRestorePreview(target, backups, staging));
        Assert.ThrowsException<InvalidDataException>(() => engine.Restore(target, backups, staging));
        Assert.AreEqual("""{"value":"current"}""", File.ReadAllText(targetSettings));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void MalformedLaterEntryDoesNotPartiallyApplyRestore()
    {
        using TestDirectory test = new();
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string firstTarget = test.CreateFile(@"target\A\settings.json", """{"value":"current-a"}""");
        string secondTarget = test.CreateFile(@"target\Z\settings.json", """{"value":"current-z"}""");
        string archivePath = Path.Combine(backups, $"settings_{DateTime.UtcNow.ToFileTimeUtc()}.ptb");
        using (FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        {
            using (StreamWriter first = new(archive.CreateEntry("A/settings.json").Open()))
            {
                first.Write("""{"value":"backup-a"}""");
            }

            using StreamWriter second = new(archive.CreateEntry("Z/settings.json").Open());
            second.Write("""{"value":""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);

        try
        {
            engine.Restore(target, backups, staging);
            Assert.Fail("Malformed restore JSON should fail before writes.");
        }
        catch (JsonException)
        {
        }

        Assert.AreEqual("""{"value":"current-a"}""", File.ReadAllText(firstTarget));
        Assert.AreEqual("""{"value":"current-z"}""", File.ReadAllText(secondTarget));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }
}
