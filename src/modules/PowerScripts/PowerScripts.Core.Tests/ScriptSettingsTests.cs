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
        _temporary = Path.Combine(Path.GetTempPath(), "ps-local-settings-" + Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_temporary, "scripts");
        Directory.CreateDirectory(_root);
        _entry = Path.Combine(_root, "action.ps1");
        File.WriteAllText(_entry, "param([string]$message)\nWrite-Output $message");
        _store = new ScriptSettingsStore(Path.Combine(_temporary, "settings"), new FileSettingsStore(Path.Combine(_temporary, "module")));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_temporary, recursive: true);

    [TestMethod]
    public void BareScriptsRequireExplicitSaveAndHelperFilesStayUndiscovered()
    {
        File.WriteAllText(Path.Combine(_root, "helper.ps1"), "# helper");
        File.WriteAllText(Path.Combine(_root, "helper.py"), "print('helper')");
        Assert.AreEqual(0, Load().Scripts.Count);
        var preview = _store.Get(_root, _entry);
        Assert.IsFalse(preview.HasLocalSettings);
        Assert.AreEqual(0, Load().Scripts.Count, "Previewing must not register or approve a file.");

        var saved = Save("""{"overrides":{}}""");
        Assert.IsFalse(saved.HasDescriptor);
        Assert.AreEqual(preview.Manifest.Id, saved.Manifest.Id);
        Assert.AreEqual(1, Load().Scripts.Count);
        Assert.AreEqual(_entry, Load().Scripts.Single().EntryFullPath);
        Assert.IsFalse(File.Exists(_entry + ToolDescriptorParser.DescriptorSuffix));
    }

    [TestMethod]
    public void BarePythonUsesExplicitPlainScriptRecipeWithoutFunctionInference()
    {
        var entry = Path.Combine(_root, "plain.py");
        File.WriteAllText(entry, "print('hello')");
        var saved = _store.Save(_root, entry, new JsonObject { ["overrides"] = new JsonObject() });
        Assert.AreEqual(ScriptRuntime.Python, saved.Manifest.Runtime);
        Assert.IsNull(saved.Manifest.EntryFunction);
        CollectionAssert.AreEqual(new[] { "python", "plain.py" }, saved.Manifest.Execute!.Command!);
        Assert.AreEqual(0, saved.ValidationErrors.Count);
    }

    [TestMethod]
    public void SparseOverridesPreserveDescriptorAndFollowUpdatedRecommendationsAfterFieldReset()
    {
        WriteDescriptor("Recommended", "Recommended description");
        var original = File.ReadAllBytes(_entry + ToolDescriptorParser.DescriptorSuffix);
        var saved = Save("""{"overrides":{"title":"Personal","x-powerscript":{"consumers":["color-picker"]}}}""");
        Assert.AreEqual("Personal", saved.Manifest.Name);
        Assert.AreEqual("Recommended description", saved.Manifest.Description);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(_entry + ToolDescriptorParser.DescriptorSuffix));
        Assert.IsFalse(saved.Overrides.ContainsKey("description"));

        WriteDescriptor("Updated recommendation", "Updated description");
        saved = _store.Get(_root, _entry);
        Assert.AreEqual("Personal", saved.Manifest.Name);
        Assert.AreEqual("Updated description", saved.Manifest.Description);
        saved = Save("""{"overrides":{"x-powerscript":{"consumers":["color-picker"]}}}""");
        Assert.AreEqual("Updated recommendation", saved.Manifest.Name);
        Assert.IsFalse(saved.Overrides.ContainsKey("title"), "Reset removes the field instead of freezing its current recommendation.");
        Assert.IsTrue(saved.Manifest.IsDiscoverableBy("COLOR-PICKER"));
        Assert.IsFalse(saved.Manifest.IsDiscoverableBy("keyboard-manager"));
        Assert.IsTrue(saved.Manifest.IsDiscoverableBy(null), "Management discovery sees the complete catalogue.");
    }

    [TestMethod]
    public void NullClearsAuthorFieldsWhileRemovingOverrideRestoresRecommendations()
    {
        WriteDescriptor();
        var metadata = JsonNode.Parse(File.ReadAllText(_entry + ToolDescriptorParser.DescriptorSuffix))!.AsObject();
        metadata["inputSchema"] = JsonNode.Parse("""{"type":"object","properties":{"message":{"type":"string","default":"hello"}}}""");
        File.WriteAllText(_entry + ToolDescriptorParser.DescriptorSuffix, metadata.ToJsonString());
        var cleared = Save("""{"overrides":{"inputSchema":{"properties":{"message":null}}}}""");
        Assert.AreEqual(0, cleared.Manifest.Parameters.Count);
        Assert.IsTrue(cleared.Overrides["inputSchema"]!["properties"]!.AsObject().ContainsKey("message"));
        Assert.AreEqual(0, _store.Get(_root, _entry).Manifest.Parameters.Count, "Null deletion markers survive persistence.");
        var reset = Save("""{"overrides":{}}""");
        Assert.AreEqual("hello", reset.Manifest.Parameters.Single().Default);
    }

    [TestMethod]
    public void NestedDeletionIsANoOpWhenTheRecommendedParentDoesNotExist()
    {
        WriteDescriptor();
        var saved = Save("""{"overrides":{"annotations":{"readOnlyHint":null}}}""");
        Assert.AreEqual(0, saved.Manifest.Parameters.Count);
        Assert.IsFalse(saved.Effective["annotations"]!.AsObject().ContainsKey("readOnlyHint"));
    }

    [TestMethod]
    public void LocalSettingsPreserveCaseInsensitiveDescriptorParsingAndCaseSensitiveParameterNames()
    {
        File.WriteAllText(_entry + ToolDescriptorParser.DescriptorSuffix, """
            {
              "NAME":"MixedCase",
              "TITLE":"Display",
              "INPUTSCHEMA":{"TYPE":"object","PROPERTIES":{"Title":{"TYPE":"string","DEFAULT":"original"}}},
              "X-POWERSCRIPT":{"INPUT":"text","OUTPUT":"text"}
            }
            """);
        var saved = Save("""{"overrides":{"title":"Local"}}""");
        Assert.AreEqual("MixedCase", saved.Manifest.Id);
        Assert.AreEqual("Local", saved.Manifest.Name);
        Assert.AreEqual("Title", saved.Manifest.Parameters.Single().Name);
        Assert.AreEqual("original", saved.Manifest.Parameters.Single().Default);
    }

    [TestMethod]
    public void EditingMetadataPreservesLegacyRestrictionsButRequiresNewAcceptanceForWeakening()
    {
        WriteDescriptor();
        var moduleSettings = new FileSettingsStore(Path.Combine(_temporary, "module"));
        new MxcSettings
        {
            Scripts = new() { ["settings-test"] = new MxcScriptOverride { Enabled = false, RiskAccepted = true } },
        }.Save(moduleSettings);
        var saved = Save("""{"overrides":{"title":"Personal"}}""");
        Assert.IsTrue(saved.HasLocalMxcSettings);
        Assert.AreEqual(false, saved.Mxc!.Enabled);
        Assert.IsFalse(saved.Mxc.RiskAccepted);
        Assert.IsTrue(Resolve(saved).Enabled);
        saved = Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        Assert.IsFalse(Resolve(saved).Enabled);
    }

    [TestMethod]
    public void EffectiveExecutionSettingsInvalidateTrustAndResetRestoresOriginalFingerprint()
    {
        WriteDescriptor();
        var initial = _store.Get(_root, _entry);
        var authorManifest = ToolDescriptorParser.TryParseFile(_entry + ToolDescriptorParser.DescriptorSuffix)!;
        authorManifest.FolderPath = _root;
        var initialHash = ScriptIntegrity.ComputeHash(initial.Manifest);
        Assert.AreEqual(ScriptIntegrity.ComputeHash(authorManifest), initialHash, "Unmodified installations preserve existing approvals.");
        var modified = Save("""{"overrides":{"x-execute":{"command":["pwsh","-File","action.ps1"]}}}""");
        Assert.AreNotEqual(initialHash, ScriptIntegrity.ComputeHash(modified.Manifest));
        Assert.AreEqual(initialHash, ScriptIntegrity.ComputeHash(Save("""{"overrides":{}}""").Manifest));
    }

    [TestMethod]
    public void MxcGrantsAreBoundToSourceContentsAndEffectiveConfiguration()
    {
        WriteDescriptor();
        var saved = Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        Assert.IsTrue(saved.HasLocalMxcSettings);
        Assert.IsTrue(saved.Mxc!.RiskAccepted);
        Assert.IsFalse(Resolve(saved).Enabled);
        File.AppendAllText(_entry, "\n# content changed");
        saved = _store.Get(_root, _entry);
        Assert.IsFalse(saved.Mxc!.RiskAccepted);
        Assert.IsTrue(Resolve(saved).Enabled);
        Assert.AreEqual(4, Resolve(saved).EnabledPolicies.Count);
        saved = Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""");
        Assert.IsFalse(Resolve(saved).Enabled);
        saved = Save("""{"overrides":{"x-powerscript":{"output":"none"}}}""");
        Assert.IsFalse(saved.Mxc!.RiskAccepted);
        Assert.IsTrue(Resolve(saved).Enabled);
    }

    [TestMethod]
    public void ResetSuppressesLegacyIdOnlyMxcAndKeepsExplicitRegistration()
    {
        var saved = Save("""{"overrides":{"title":"Personal"},"mxc":{"enabled":false,"riskAccepted":true}}""");
        var legacy = new MxcSettings
        {
            Scripts = new() { [saved.Manifest.Id] = new MxcScriptOverride { Enabled = false, RiskAccepted = true } },
        };
        saved = _store.Reset(_root, _entry);
        Assert.AreEqual(0, saved.Overrides.Count);
        Assert.IsTrue(saved.HasLocalMxcSettings);
        Assert.IsNull(saved.Mxc);
        Assert.AreEqual(1, Load().Scripts.Count);
        Assert.IsTrue(MxcPolicyResolver.Resolve(legacy, saved.Manifest.Id, hasLocalOverride: true, localOverride: saved.Mxc).Enabled);
    }

    [TestMethod]
    public void SourceAndRootAssociationPreventsCopiedPreferencesFromFollowingAnId()
    {
        WriteDescriptor();
        Save("""{"overrides":{"title":"Personal"},"mxc":{"enabled":false,"riskAccepted":true}}""");
        var other = Path.Combine(_root, "other.ps1");
        File.Copy(_entry, other);
        File.Copy(_entry + ToolDescriptorParser.DescriptorSuffix, other + ToolDescriptorParser.DescriptorSuffix);
        var target = _store.GetSettingsPath(_root, other);
        File.Copy(_store.GetSettingsPath(_root, _entry), target);
        Assert.ThrowsException<InvalidDataException>(() => _store.Get(_root, other));
        Assert.AreEqual(1, Load().Scripts.Count, "The mismatched record must not fall back to a runnable recommendation.");
        Assert.IsTrue(Load().Errors.Any(error => error.Message.Contains("source path", StringComparison.Ordinal)));

        var anotherRoot = Path.Combine(_temporary, "another-root");
        Directory.CreateDirectory(anotherRoot);
        var otherRegistry = new ScriptRegistry(anotherRoot, _store);
        otherRegistry.Load();
        Assert.AreEqual(0, otherRegistry.Scripts.Count);
        Assert.IsFalse(_store.Get(anotherRoot, _entry).HasLocalSettings);
    }

    [TestMethod]
    public void MissingRegisteredSourceStaysVisibleButNeverRunnable()
    {
        Save("""{"overrides":{}}""");
        File.Delete(_entry);
        var registry = Load();
        Assert.AreEqual(0, registry.Scripts.Count);
        Assert.AreEqual(1, registry.UnconfiguredScripts.Count);
        Assert.IsTrue(registry.Errors.Any(error => error.Message.Contains("not found", StringComparison.Ordinal)));
        _store.Forget(_root, _entry);
        Assert.AreEqual(0, Load().UnconfiguredScripts.Count);
        Assert.IsFalse(File.Exists(_store.GetSettingsPath(_root, _entry)));
    }

    [DataTestMethod]
    [DataRow("""{"overrides":{"x-powerscript":{"consumers":[]}}}""")]
    [DataRow("""{"overrides":{"x-powerscript":{"consumers":["not a module"]}}}""")]
    [DataRow("""{"overrides":{"x-powerscript":{"runtime":"unknown"}}}""")]
    [DataRow("""{"overrides":{"x-execute":{"command":[]}}}""")]
    [DataRow("""{"overrides":{"x-execute":{"command":["pwsh"],"stdin":"not-json"}}}""")]
    [DataRow("""{"overrides":{"x-powerscript":{"input":"files","minFiles":3,"maxFiles":-1}}}""")]
    [DataRow("""{"mxc":{"enabledPolicies":["unknown"]}}""")]
    [DataRow("""{"overrides":{"inputSchema":{"properties":{"count":{"type":"integer","minimum":1,"maximum":3,"default":4}}}}}""")]
    [DataRow("""{"overrides":{"inputSchema":{"properties":{"switch":{"type":"boolean","default":"yes"}}}}}""")]
    public void InvalidSetupNeverPartiallyReplacesSavedSettings(string request)
    {
        WriteDescriptor();
        Save("""{"overrides":{"title":"Previous"}}""");
        var before = File.ReadAllBytes(_store.GetSettingsPath(_root, _entry));
        Assert.ThrowsException<InvalidDataException>(() => Save(request));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(_store.GetSettingsPath(_root, _entry)));
        Assert.AreEqual("Previous", _store.Get(_root, _entry).Manifest.Name);
    }

    [TestMethod]
    public void OversizedRecordAndFutureSchemaAreRejectedWithoutOverwriting()
    {
        Save("""{"overrides":{}}""");
        var file = _store.GetSettingsPath(_root, _entry);
        var before = File.ReadAllBytes(file);
        Assert.ThrowsException<InvalidDataException>(() => _store.Save(_root, _entry, new JsonObject
        {
            ["overrides"] = new JsonObject { ["title"] = new string('x', ScriptSettingsStore.MaximumRecordBytes) },
        }));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        var future = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        future["schemaVersion"] = 99;
        File.WriteAllText(file, future.ToJsonString());
        before = File.ReadAllBytes(file);
        Assert.ThrowsException<InvalidDataException>(() => Save("""{"overrides":{}}"""));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
    }

    [TestMethod]
    public async Task ConcurrentMetadataAndPolicyUpdatesPreserveBothSections()
    {
        WriteDescriptor();
        await Task.WhenAll(
            Task.Run(() => Save("""{"overrides":{"title":"Personal"}}""")),
            Task.Run(() => Save("""{"mxc":{"enabled":false,"riskAccepted":true}}""")));
        var saved = _store.Get(_root, _entry);
        Assert.AreEqual("Personal", saved.Manifest.Name);
        Assert.AreEqual(false, saved.Mxc!.Enabled);
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(saved.SettingsPath)!, "*.tmp").Length);
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

    private void WriteDescriptor(string title = "Recommended", string description = "Description") =>
        File.WriteAllText(_entry + ToolDescriptorParser.DescriptorSuffix, new JsonObject
        {
            ["name"] = "settings-test",
            ["title"] = title,
            ["description"] = description,
        }.ToJsonString());
}
