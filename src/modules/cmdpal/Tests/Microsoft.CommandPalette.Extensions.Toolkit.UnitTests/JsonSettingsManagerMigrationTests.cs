// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class JsonSettingsManagerMigrationTests
{
    private string _tempDir = string.Empty;

    /// <summary>
    /// Concrete subclass that exposes the protected migration method for testing.
    /// </summary>
    private sealed class TestSettingsManager : JsonSettingsManager
    {
        public TestSettingsManager(string filePath)
        {
            FilePath = filePath;
        }

        public void CallMigrateFromLegacyFile(string legacyFilePath)
        {
            MigrateFromLegacyFile(legacyFilePath);
        }
    }

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CmdPalTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
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
    public void MigrateFromLegacyFile_LegacyMissing_DoesNotCreateExtensionFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var mgr = new TestSettingsManager(extPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.CallMigrateFromLegacyFile(Path.Combine(_tempDir, "nonexistent.json"));

        Assert.IsFalse(File.Exists(extPath));
    }

    [TestMethod]
    public void MigrateFromLegacyFile_PerExtensionFileAlreadyExists_DoesNotOverwrite()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        File.WriteAllText(extPath, "{}");

        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"fromLegacy\"}");

        var mgr = new TestSettingsManager(extPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.CallMigrateFromLegacyFile(legacyPath);

        Assert.AreEqual("{}", File.ReadAllText(extPath));
    }

    [TestMethod]
    public void MigrateFromLegacyFile_LegacyPresent_MigratesOwnedKeys()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"migrated\", \"otherKey\": \"ignored\"}");

        var mgr = new TestSettingsManager(extPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.CallMigrateFromLegacyFile(legacyPath);

        Assert.IsTrue(File.Exists(extPath));
        var content = File.ReadAllText(extPath);
        var json = JsonNode.Parse(content)!.AsObject();
        Assert.IsTrue(json.ContainsKey("key1"));
        Assert.AreEqual("migrated", json["key1"]!.GetValue<string>());
        Assert.IsFalse(json.ContainsKey("otherKey"));
    }

    [TestMethod]
    public void MigrateFromLegacyFile_InvalidJson_DoesNotThrowOrCreateFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "not valid json {{{");

        var mgr = new TestSettingsManager(extPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.CallMigrateFromLegacyFile(legacyPath);

        Assert.IsFalse(File.Exists(extPath));
    }

    [TestMethod]
    public void MigrateFromLegacyFile_EmptyFilePath_NoOp()
    {
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"value\"}");

        var mgr = new TestSettingsManager(string.Empty);

        mgr.CallMigrateFromLegacyFile(legacyPath);

        // Should not throw or create any file
    }

    [TestMethod]
    public void MigrateFromLegacyFile_EmptyLegacyPath_NoOp()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var mgr = new TestSettingsManager(extPath);

        mgr.CallMigrateFromLegacyFile(string.Empty);

        Assert.IsFalse(File.Exists(extPath));
    }

    [TestMethod]
    public void MigrateFromLegacyFile_LegacyIsJsonArray_DoesNotCreateFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "[1, 2, 3]");

        var mgr = new TestSettingsManager(extPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.CallMigrateFromLegacyFile(legacyPath);

        Assert.IsFalse(File.Exists(extPath));
    }
}
