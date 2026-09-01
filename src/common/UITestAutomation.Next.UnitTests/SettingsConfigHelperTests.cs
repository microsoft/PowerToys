// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public class SettingsConfigHelperTests
{
    private static readonly string[] ExpectedModuleNames =
    [
        "AdvancedPaste",
        "AlwaysOnTop",
        "Awake",
        "CmdNotFound",
        "CmdPal",
        "ColorPicker",
        "CropAndLock",
        "CursorWrap",
        "EnvironmentVariables",
        "FancyZones",
        "File Explorer Preview",
        "File Locksmith",
        "FindMyMouse",
        "GrabAndMove",
        "Hosts",
        "Image Resizer",
        "Keyboard Manager",
        "LightSwitch",
        "Measure Tool",
        "MouseHighlighter",
        "MouseJump",
        "MousePointerCrosshairs",
        "MouseWithoutBorders",
        "NewPlus",
        "Peek",
        "PowerDisplay",
        "PowerRename",
        "PowerToys Run",
        "QuickAccent",
        "RegistryPreview",
        "Shortcut Guide",
        "TextExtractor",
        "Workspaces",
        "ZoomIt",
    ];

    [TestMethod]
    public void ConfigureGlobalModuleSettingsSeedsExactFreshProfileBaseline()
    {
        var root = new JsonObject();

        SettingsConfigHelper.ConfigureGlobalModuleSettings(root, "Image Resizer");

        var enabled = root["enabled"]!.AsObject();
        CollectionAssert.AreEquivalent(ExpectedModuleNames, enabled.Select(property => property.Key).ToArray());
        foreach (var moduleName in ExpectedModuleNames)
        {
            Assert.AreEqual(moduleName == "Image Resizer", enabled[moduleName]!.GetValue<bool>(), moduleName);
        }
    }

    [TestMethod]
    public void ConfigureGlobalModuleSettingsHandlesUnknownModuleKeys()
    {
        var root = new JsonObject
        {
            ["enabled"] = new JsonObject
            {
                ["ExistingFutureModule"] = true,
            },
        };

        SettingsConfigHelper.ConfigureGlobalModuleSettings(root, "RequestedFutureModule");

        var enabled = root["enabled"]!.AsObject();
        Assert.IsFalse(enabled["ExistingFutureModule"]!.GetValue<bool>());
        Assert.IsTrue(enabled["RequestedFutureModule"]!.GetValue<bool>());
    }

    [TestMethod]
    public void PreserveFileRestoresExistingBytes()
    {
        var root = CreateTemporaryDirectory();
        var path = Path.Combine(root, "module", "settings.json");
        var original = new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, original);

            using (SettingsConfigHelper.PreserveFile(path))
            {
                File.WriteAllText(path, "changed");
            }

            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PreserveFileDeletesFileCreatedInsideScope()
    {
        var root = CreateTemporaryDirectory();
        var path = Path.Combine(root, "module", "settings.json");

        try
        {
            using (SettingsConfigHelper.PreserveFile(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "created by test");
            }

            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PreserveFirstRunSettingsRestoresExistingFiles()
    {
        var root = CreateTemporaryDirectory();
        var settingsPath = Path.Combine(root, "settings.json");
        var oobePath = Path.Combine(root, "oobe_settings.json");
        var originalSettings = new byte[] { 1, 2, 3 };
        var originalOobe = new byte[] { 4, 5, 6 };

        try
        {
            File.WriteAllBytes(settingsPath, originalSettings);
            File.WriteAllBytes(oobePath, originalOobe);

            using (SettingsConfigHelper.PreserveFirstRunSettings(root))
            {
                File.WriteAllText(settingsPath, "changed settings");
                File.WriteAllText(oobePath, "changed oobe");
            }

            CollectionAssert.AreEqual(originalSettings, File.ReadAllBytes(settingsPath));
            CollectionAssert.AreEqual(originalOobe, File.ReadAllBytes(oobePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PreserveFirstRunSettingsDeletesCreatedFiles()
    {
        var root = CreateTemporaryDirectory();
        var settingsPath = Path.Combine(root, "settings.json");
        var oobePath = Path.Combine(root, "oobe_settings.json");

        try
        {
            using (SettingsConfigHelper.PreserveFirstRunSettings(root))
            {
                File.WriteAllText(settingsPath, "created settings");
                File.WriteAllText(oobePath, "created oobe");
            }

            Assert.IsFalse(File.Exists(settingsPath));
            Assert.IsFalse(File.Exists(oobePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PreserveFirstRunSettingsRestoresFirstSnapshotWhenSecondFails()
    {
        var root = CreateTemporaryDirectory();
        var settingsPath = Path.Combine(root, "settings.json");
        var oobePath = Path.Combine(root, "oobe_settings.json");
        var originalSettings = new byte[] { 1, 2, 3 };

        try
        {
            File.WriteAllBytes(settingsPath, originalSettings);
            File.WriteAllText(oobePath, "locked");

            using var lockedOobe = new FileStream(oobePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.ThrowsExactly<IOException>(() => SettingsConfigHelper.PreserveFirstRunSettings(root));
            CollectionAssert.AreEqual(originalSettings, File.ReadAllBytes(settingsPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PowerToys-UITestAutomationNext-UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
