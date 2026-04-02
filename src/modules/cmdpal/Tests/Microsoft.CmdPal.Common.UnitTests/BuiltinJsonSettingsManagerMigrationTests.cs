// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common.UnitTests;

[TestClass]
public class BuiltinJsonSettingsManagerMigrationTests
{
    private string _tempDir = string.Empty;

    /// <summary>
    /// Concrete subclass that exposes migration through the public load path for testing.
    /// </summary>
    private sealed class TestSettingsManager : BuiltinJsonSettingsManager
    {
        public TestSettingsManager(string filePath, string? legacyFilePath = null)
        {
            FilePath = filePath;

            if (!string.IsNullOrEmpty(legacyFilePath))
            {
                EnableMigration(legacyFilePath);
            }
        }
    }

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Directory.CreateTempSubdirectory("CmdPalTests").FullName;
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
    public void LoadSettings_WithMissingLegacyFile_DoesNotCreateExtensionFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var mgr = new TestSettingsManager(extPath, Path.Combine(_tempDir, "nonexistent.json"));
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.LoadSettings();

        Assert.IsFalse(File.Exists(extPath));
    }

    [TestMethod]
    public void LoadSettings_WithExistingExtensionFile_DoesNotOverwrite()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        File.WriteAllText(extPath, "{}");

        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"fromLegacy\"}");

        var mgr = new TestSettingsManager(extPath, legacyPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.LoadSettings();

        Assert.AreEqual("{}", File.ReadAllText(extPath));
    }

    [TestMethod]
    public void LoadSettings_WithLegacySettingsFile_MigratesOwnedKeysToExtensionFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"migrated\", \"otherKey\": \"ignored\"}");

        var mgr = new TestSettingsManager(extPath, legacyPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.LoadSettings();

        Assert.IsTrue(File.Exists(extPath));
        var content = File.ReadAllText(extPath);
        var json = JsonNode.Parse(content)!.AsObject();
        Assert.IsTrue(json.ContainsKey("key1"));
        Assert.AreEqual("migrated", json["key1"]!.GetValue<string>());
        Assert.IsFalse(json.ContainsKey("otherKey"));
    }

    [TestMethod]
    public void LoadSettings_WithLegacySettingsFile_LoadsMigratedValueIntoSettings()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"migrated\", \"otherKey\": \"ignored\"}");

        var mgr = new TestSettingsManager(extPath, legacyPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.LoadSettings();

        Assert.AreEqual("migrated", mgr.Settings.GetSetting<string>("key1"));
    }

    [TestMethod]
    public void LoadSettings_WithInvalidLegacyJson_DoesNotThrowOrCreateFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "not valid json {{{");

        var mgr = new TestSettingsManager(extPath, legacyPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.LoadSettings();

        Assert.IsFalse(File.Exists(extPath));
    }

    [TestMethod]
    public void LoadSettings_WithEmptyFilePath_ThrowsInvalidOperationException()
    {
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "{\"key1\": \"value\"}");

        var mgr = new TestSettingsManager(string.Empty, legacyPath);

        Assert.ThrowsException<InvalidOperationException>(() => mgr.LoadSettings());
    }

    [TestMethod]
    public void LoadSettings_WithoutMigrationEnabled_DoesNotCreateExtensionFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var mgr = new TestSettingsManager(extPath);

        mgr.LoadSettings();

        Assert.IsFalse(File.Exists(extPath));
    }

    [TestMethod]
    public void LoadSettings_WithLegacyJsonArray_DoesNotCreateFile()
    {
        var extPath = Path.Combine(_tempDir, "ext.settings.json");
        var legacyPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(legacyPath, "[1, 2, 3]");

        var mgr = new TestSettingsManager(extPath, legacyPath);
        mgr.Settings.Add(new TextSetting("key1", "default"));

        mgr.LoadSettings();

        Assert.IsFalse(File.Exists(extPath));
    }
}
