// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ScriptSettingsTests
{
    private string _temporary = string.Empty;
    private string _root = string.Empty;
    private string _entry = string.Empty;
    private ScriptSettingsStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _temporary = Path.Combine(Path.GetTempPath(), "ps-policy-settings-" + Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_temporary, "scripts");
        Directory.CreateDirectory(_root);
        _entry = Path.Combine(_root, "action.ps1");
        File.WriteAllText(_entry, "Write-Output 'hello'");
        _store = new ScriptSettingsStore(Path.Combine(_temporary, "settings"), new FileSettingsStore(Path.Combine(_temporary, "module")));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_temporary, recursive: true);

    [TestMethod]
    public void BareScriptsAndHelpersRequireDescriptorsEvenWithLocalSettings()
    {
        Assert.ThrowsException<InvalidDataException>(() => Save("""{"mxc":null}"""));
        Assert.IsFalse(File.Exists(_store.GetSettingsPath(_root, _entry)));
        Assert.AreEqual(0, Load().Scripts.Count);
        WriteLegacyRecord(new JsonObject { ["title"] = "Old registration" });
        Assert.AreEqual(0, Load().Scripts.Count);
        Assert.IsTrue(Load().UnconfiguredScripts.Single().ConfigurationErrors.Any(error => error.Contains("descriptor", StringComparison.Ordinal)));
        _store.Forget(_root, _entry);
        Assert.AreEqual(0, Load().UnconfiguredScripts.Count);
    }

    [TestMethod]
    public void MetadataOverridesAreRejectedWithoutChangingDescriptorOrSecurityRecord()
    {
        WriteDescriptor();
        Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        var before = File.ReadAllBytes(_store.GetSettingsPath(_root, _entry));
        var descriptor = File.ReadAllBytes(_entry + ToolDescriptorParser.DescriptorSuffix);
        Assert.ThrowsException<InvalidDataException>(() => Save("""{"overrides":{"title":"Personal"}}"""));
        Assert.ThrowsException<InvalidDataException>(() => Save("""{"overrides":{}}"""));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(_store.GetSettingsPath(_root, _entry)));
        CollectionAssert.AreEqual(descriptor, File.ReadAllBytes(_entry + ToolDescriptorParser.DescriptorSuffix));
    }

    [TestMethod]
    public void LegacyMetadataIsIgnoredAndAuthorEditsAreImmediatelyEffective()
    {
        WriteDescriptor();
        WriteLegacyRecord(JsonNode.Parse("""{"title":"Personal","x-powerscript":{"output":"none","consumers":["color-picker"]}}""")!.AsObject());
        var snapshot = _store.Get(_root, _entry);
        Assert.AreEqual("Author title", snapshot.Manifest.Name);
        Assert.AreEqual(Execution.PowerScriptDataFormat.Text, snapshot.Manifest.OutputFormat);
        Assert.IsNull(snapshot.Manifest.Consumers);
        Assert.AreEqual(0, snapshot.Overrides.Count);
        var originalHash = ScriptIntegrity.ComputeHash(snapshot.Manifest);
        var author = ToolDescriptorParser.TryParseFile(_entry + ToolDescriptorParser.DescriptorSuffix)!;
        author.FolderPath = _root;
        Assert.AreEqual(ScriptIntegrity.ComputeHash(author), originalHash);
        WriteDescriptor("Updated title");
        Assert.AreEqual("Updated title", _store.Get(_root, _entry).Manifest.Name);
        Assert.AreNotEqual(originalHash, ScriptIntegrity.ComputeHash(_store.Get(_root, _entry).Manifest));
    }

    [TestMethod]
    public void MxcGrantsRemainContentBoundAcrossSourceAndDescriptorChanges()
    {
        WriteDescriptor();
        var saved = Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        Assert.IsFalse(Resolve(saved).Enabled);
        File.AppendAllText(_entry, "\n# changed");
        Assert.IsTrue(Resolve(_store.Get(_root, _entry)).Enabled);
        Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        WriteDescriptor("Changed metadata");
        Assert.IsTrue(Resolve(_store.Get(_root, _entry)).Enabled);
    }

    [TestMethod]
    public void ResetSuppressesLegacyRelaxationsWithoutRewritingMetadata()
    {
        WriteDescriptor();
        Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        var descriptor = File.ReadAllBytes(_entry + ToolDescriptorParser.DescriptorSuffix);
        var saved = _store.Reset(_root, _entry);
        var legacy = new MxcSettings { Scripts = new() { ["settings-test"] = new MxcScriptOverride { Enabled = false, RiskAccepted = true } } };
        Assert.IsTrue(MxcPolicyResolver.Resolve(legacy, saved.Manifest.Id, hasLocalOverride: true, localOverride: saved.Mxc).Enabled);
        Assert.IsNull(saved.Mxc);
        Assert.AreEqual(1, Load().Scripts.Count);
        CollectionAssert.AreEqual(descriptor, File.ReadAllBytes(_entry + ToolDescriptorParser.DescriptorSuffix));
    }

    [TestMethod]
    public void CopiedSecuritySettingsCannotFollowAnotherSourceOrCatalogue()
    {
        WriteDescriptor();
        Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        var other = Path.Combine(_root, "other.ps1");
        File.Copy(_entry, other);
        File.Copy(_store.GetSettingsPath(_root, _entry), _store.GetSettingsPath(_root, other));
        Assert.ThrowsException<InvalidDataException>(() => _store.Get(_root, other));
        Assert.IsFalse(_store.Get(Path.Combine(_temporary, "another-root"), _entry).HasLocalSettings);
    }

    [TestMethod]
    public void InvalidAndFutureSecurityRecordsAreRejectedWithoutReplacingSavedData()
    {
        WriteDescriptor();
        Save("""{"mxc":null}""");
        var path = _store.GetSettingsPath(_root, _entry);
        var before = File.ReadAllBytes(path);
        Assert.ThrowsException<InvalidDataException>(() => Save("""{"mxc":{"enabledPolicies":["unknown"]}}"""));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        var future = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        future["schemaVersion"] = 99;
        File.WriteAllText(path, future.ToJsonString());
        Assert.ThrowsException<InvalidDataException>(() => Save("""{"mxc":null}"""));
    }

    private void WriteLegacyRecord(JsonObject overrides)
    {
        var path = _store.GetSettingsPath(_root, _entry);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["scriptsRoot"] = _root,
            ["entryPath"] = _entry,
            ["overrides"] = overrides,
        }.ToJsonString());
    }

    private ScriptRegistry Load()
    {
        var registry = new ScriptRegistry(_root, _store);
        registry.Load();
        return registry;
    }

    private ScriptSettingsSnapshot Save(string request) => _store.Save(_root, _entry, JsonNode.Parse(request)!.AsObject());

    private static MxcEffectivePolicy Resolve(ScriptSettingsSnapshot snapshot) => MxcPolicyResolver.Resolve(
        new MxcSettings(), snapshot.Manifest.Id, hasLocalOverride: snapshot.HasLocalMxcSettings, localOverride: snapshot.Mxc);

    private void WriteDescriptor(string title = "Author title") =>
        File.WriteAllText(_entry + ToolDescriptorParser.DescriptorSuffix, new JsonObject
        {
            ["name"] = "settings-test",
            ["title"] = title,
        }.ToJsonString());
}
