// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
[DoNotParallelize]
public sealed class ZoomItStateTests
{
    private readonly string registryPath = @"Software\PowerToysUITests\ZoomItState-" + Guid.NewGuid().ToString("N");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ZoomItStateTests-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void Initialize() => Directory.CreateDirectory(directory);

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            ZoomItState.RestorePending();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(registryPath, throwOnMissingSubKey: false);
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MissingCleanupDoesNotBecomeTheNextTestsOriginalState()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
        {
            key.SetValue("ToggleKey", 0x241, RegistryValueKind.DWord);
        }

        ZoomItState.CaptureForTest(Path.Combine(directory, "first.json"), registryPath);
        ZoomItState.CaptureForTest(Path.Combine(directory, "second.json"), registryPath);
        using var backup = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "second.json")));
        var saved = backup.RootElement.GetProperty("Values").EnumerateArray().Single(value => value.GetProperty("Name").GetString() == "ToggleKey");
        Assert.AreEqual(0x241, saved.GetProperty("Value").GetInt32(), "An abandoned initialization must not replace the recoverable original hotkey.");
        ZoomItState.RestorePending();
        using var restored = Registry.CurrentUser.OpenSubKey(registryPath);
        Assert.IsNotNull(restored);
        Assert.AreEqual(0x241, restored.GetValue("ToggleKey"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestorationPreservesOriginalKeyExistence(bool existed)
    {
        if (existed)
        {
            Registry.CurrentUser.CreateSubKey(registryPath).Dispose();
        }

        using (var state = ZoomItState.CaptureForTest(Path.Combine(directory, "original.json"), registryPath))
        {
            using var seeded = Registry.CurrentUser.OpenSubKey(registryPath);
            Assert.IsNotNull(seeded);
            Assert.IsTrue(seeded.ValueCount > 0);
        }

        ZoomItState.RestorePending();
        using var restored = Registry.CurrentUser.OpenSubKey(registryPath);
        Assert.AreEqual(existed, restored is not null);
        if (restored is not null)
        {
            Assert.AreEqual(0, restored.ValueCount, "An originally empty key must remain empty, not be deleted.");
        }
    }

    [TestMethod]
    public void BackupAndRestorePreserveValueKindsAndData()
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        key.SetValue("ToggleKey", 0x241, RegistryValueKind.DWord);
        key.SetValue("Text", "original \u03a9", RegistryValueKind.String);
        key.SetValue("Expanded", @"%TEMP%\ZoomIt.txt", RegistryValueKind.ExpandString);
        byte[] binary = [0, 128, 255];
        key.SetValue("Binary", binary, RegistryValueKind.Binary);
        string[] multiple = ["alpha", "beta"];
        key.SetValue("Multiple", multiple, RegistryValueKind.MultiString);
        key.SetValue("Large", 0x123456789abcdefL, RegistryValueKind.QWord);
        var original = key.GetValueNames().ToDictionary(
            name => name,
            name => (Value: key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), Kind: key.GetValueKind(name)));
        var path = Path.Combine(directory, "original.json");
        using (ZoomItState.CaptureForTest(path, registryPath))
        {
            using var backup = JsonDocument.Parse(File.ReadAllText(path));
            Assert.IsTrue(backup.RootElement.GetProperty("KeyExisted").GetBoolean());
            var values = backup.RootElement.GetProperty("Values").EnumerateArray().ToDictionary(value => value.GetProperty("Name").GetString()!);
            Assert.AreEqual(0x241, values["ToggleKey"].GetProperty("Value").GetInt32());
            Assert.AreEqual(@"%TEMP%\ZoomIt.txt", values["Expanded"].GetProperty("Value").GetString());
            CollectionAssert.AreEqual(binary, values["Binary"].GetProperty("Value").GetBytesFromBase64());
            CollectionAssert.AreEqual(multiple, values["Multiple"].GetProperty("Value").EnumerateArray().Select(value => value.GetString()).ToArray());
            Assert.AreEqual(0x123456789abcdefL, values["Large"].GetProperty("Value").GetInt64());
        }

        CollectionAssert.AreEquivalent(original.Keys.ToArray(), key.GetValueNames());
        foreach (var (name, entry) in original)
        {
            Assert.AreEqual(entry.Kind, key.GetValueKind(name));
            var actual = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (entry.Value is Array expected)
            {
                if (actual is Array actualArray)
                {
                    CollectionAssert.AreEqual(expected, actualArray);
                }
                else
                {
                    Assert.Fail($"Registry value {name} did not restore its array data.");
                }
            }
            else
            {
                Assert.AreEqual(entry.Value, actual);
            }
        }
    }

    [TestMethod]
    public void FailedBackupDoesNotMutateRegistry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        key.SetValue("ToggleKey", 0x241, RegistryValueKind.DWord);
        var path = Path.Combine(directory, "existing.json");
        File.WriteAllText(path, "Preserve the existing backup.");
        Assert.ThrowsExactly<IOException>(() => ZoomItState.CaptureForTest(path, registryPath));
        Assert.AreEqual(0x241, key.GetValue("ToggleKey"));
        Assert.AreEqual(1, key.ValueCount);
        Assert.AreEqual("Preserve the existing backup.", File.ReadAllText(path));
    }
}
