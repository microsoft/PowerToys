// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

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
            Kind = ScriptKind.System,
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
}
