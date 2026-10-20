// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Tests;

[TestClass]
public class SecurityTests
{
    private string _folder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "powerscripts-sec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private PowerScriptManifest WriteScript(string id, string body, params string[] capabilities)
    {
        var entry = "run.ps1";
        File.WriteAllText(Path.Combine(_folder, entry), body);
        return new PowerScriptManifest
        {
            Id = id,
            Name = id,
            Entry = entry,
            FolderPath = _folder,
            Capabilities = capabilities.ToList(),
        };
    }

    [TestMethod]
    public void Integrity_IsStable_ForSameContent()
    {
        var a = WriteScript("s", "Write-Host hi");
        var first = ScriptIntegrity.ComputeHash(a);
        var second = ScriptIntegrity.ComputeHash(a);
        Assert.AreEqual(first, second);
        Assert.AreNotEqual(string.Empty, first);
    }

    [TestMethod]
    public void Integrity_Changes_WhenBodyChanges()
    {
        var a = WriteScript("s", "Write-Host hi");
        var before = ScriptIntegrity.ComputeHash(a);

        File.WriteAllText(Path.Combine(_folder, "run.ps1"), "Remove-Item C:\\ -Recurse");
        var after = ScriptIntegrity.ComputeHash(a);

        Assert.AreNotEqual(before, after);
    }

    [TestMethod]
    public void Integrity_Changes_WhenCapabilitiesChange()
    {
        var a = WriteScript("s", "Write-Host hi", "fileRead");
        var before = ScriptIntegrity.ComputeHash(a);

        var b = WriteScript("s", "Write-Host hi", "fileRead", "process");
        var after = ScriptIntegrity.ComputeHash(b);

        Assert.AreNotEqual(before, after);
    }

    [TestMethod]
    public void Integrity_Changes_WhenDescriptorLaunchRecipeChanges()
    {
        var manifest = WriteScript("s", "Write-Host hi");
        var descriptorPath = manifest.EntryFullPath + ToolDescriptorParser.DescriptorSuffix;
        File.WriteAllText(descriptorPath, """{"name":"s","x-execute":{"command":["pwsh","run.ps1"]}}""");
        var before = ScriptIntegrity.ComputeHash(manifest);

        File.WriteAllText(descriptorPath, """{"name":"s","x-execute":{"command":["cmd","/c","whoami"]}}""");
        var after = ScriptIntegrity.ComputeHash(manifest);

        Assert.AreNotEqual(before, after);
    }

    [TestMethod]
    public void TrustStore_RoundTrips_And_Enforces_Hash()
    {
        var path = Path.Combine(_folder, "trust.json");
        var manifest = WriteScript("s", "Write-Host hi");
        var hash = ScriptIntegrity.ComputeHash(manifest);

        var store = new TrustStore(path);
        Assert.IsFalse(store.IsTrusted("s", hash));

        store.Trust(new TrustRecord { Id = "s", Hash = hash, ApprovedUtc = DateTimeOffset.UtcNow });
        Assert.IsTrue(store.IsTrusted("s", hash));

        // A different content hash for the same id is NOT trusted (edit invalidates approval).
        Assert.IsFalse(store.IsTrusted("s", "deadbeef"));

        // Persisted across instances.
        var reopened = new TrustStore(path);
        Assert.IsTrue(reopened.IsTrusted("s", hash));

        // Revoke clears it.
        Assert.IsTrue(reopened.Revoke("s"));
        Assert.IsFalse(new TrustStore(path).IsTrusted("s", hash));
    }

    [TestMethod]
    public void FileSettingsStore_RoundTrips_Blobs()
    {
        var store = new FileSettingsStore(_folder);

        // Absent blob reads as null.
        Assert.IsNull(store.ReadBlob("config.json"));

        store.WriteBlob("config.json", "{\"scriptsRoot\":\"C:/demo\"}");
        Assert.AreEqual("{\"scriptsRoot\":\"C:/demo\"}", store.ReadBlob("config.json"));

        // Overwrite replaces the blob and lands at the expected file path.
        store.WriteBlob("config.json", "{}");
        Assert.AreEqual("{}", store.ReadBlob("config.json"));
        Assert.IsTrue(File.Exists(Path.Combine(_folder, "config.json")));
    }

    [TestMethod]
    public void TrustStore_UsesInjectedSettingsStore()
    {
        // The seam that lets the trust store move to the protected settings store later: swap the
        // ISettingsStore and every caller is unaffected.
        var store = new FileSettingsStore(_folder);
        var manifest = WriteScript("s", "Write-Host hi");
        var hash = ScriptIntegrity.ComputeHash(manifest);

        var trust = new TrustStore(store);
        trust.Trust(new TrustRecord { Id = "s", Hash = hash, ApprovedUtc = DateTimeOffset.UtcNow });

        // Persisted through the store (default blob key = trust.json).
        Assert.IsTrue(new TrustStore(store).IsTrusted("s", hash));
        Assert.IsNotNull(store.ReadBlob(PowerScripts.Core.PowerScriptsPaths.TrustFileName));
    }
}
