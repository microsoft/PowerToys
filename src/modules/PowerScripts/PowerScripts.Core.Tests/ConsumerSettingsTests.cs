// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class ConsumerSettingsTests
{
    [TestMethod]
    public void EffectiveConsumerStateHonorsEveryParentPolicyAndPreferenceCombination()
    {
        bool?[] policies = [null, false, true];
        foreach (var id in new[] { "advanced-paste", "keyboard-manager", "command-palette" })
        {
            foreach (var moduleUser in new[] { false, true })
            {
                foreach (var user in new[] { false, true })
                {
                    foreach (var modulePolicy in policies)
                    {
                        foreach (var parentPolicy in policies)
                        {
                            foreach (var consumerPolicy in policies)
                            {
                                var store = new MemoryStore();
                                store.WriteBlob("config.json", new JsonObject
                                {
                                    ["consumers"] = new JsonObject { [id] = user },
                                }.ToJsonString());
                                var parent = id switch
                                {
                                    "advanced-paste" => "AdvancedPaste",
                                    "keyboard-manager" => "KeyboardManager",
                                    "command-palette" => "CmdPal",
                                    _ => throw new InvalidOperationException(),
                                };
                                bool? Read(string key, string name) =>
                                    key == PowerScriptsPolicies.ConsumersKey ? consumerPolicy :
                                    name == "ConfigureEnabledUtilityPowerScripts" ? modulePolicy :
                                    name == $"ConfigureEnabledUtility{parent}" ? parentPolicy : null;
                                var settings = new ConsumerSettings(store, Read, () => moduleUser, () => false);
                                var state = settings.Get(id);
                                var parentsEnabled = (modulePolicy ?? moduleUser) && parentPolicy != false;
                                var scenario = $"{id}, user={user}, module={moduleUser}/{modulePolicy}, parent={parentPolicy}, consumer={consumerPolicy}";
                                Assert.AreEqual(parentsEnabled && (consumerPolicy ?? user), state.IsEnabled, scenario);
                                Assert.AreEqual(parentsEnabled && !consumerPolicy.HasValue, state.CanChange, scenario);
                                Assert.AreEqual(user, state.UserEnabled, scenario);
                                Assert.AreEqual(consumerPolicy.HasValue || parentPolicy == false, state.IsPolicyControlled, scenario);
                                Assert.AreEqual(
                                    !(modulePolicy ?? moduleUser) ? "module-disabled" : parentPolicy == false ? "parent-policy-disabled" : null,
                                    state.DisabledReason,
                                    scenario);
                            }
                        }
                    }
                }
            }
        }
    }

    [TestMethod]
    public void DefaultsPreserveExistingKeyboardBindingsAndAdvancedPasteOptIn()
    {
        var store = new MemoryStore();
        var settings = new ConsumerSettings(store, (_, _) => null, () => true, () => false);
        Assert.IsFalse(settings.Get("advanced-paste").IsEnabled);
        Assert.IsFalse(settings.Get("file-explorer").IsEnabled);
        Assert.IsTrue(settings.Get("keyboard-manager").IsEnabled);
        Assert.IsTrue(settings.Get("command-palette").IsEnabled);
        Assert.IsTrue(settings.Get("future-module").IsEnabled);
        Assert.IsNull(store.ReadBlob("config.json"), "Discovery must not write preferences.");

        settings = new ConsumerSettings(store, (_, _) => null, () => true, () => true);
        Assert.IsTrue(settings.Get("advanced-paste").IsEnabled);
    }

    [TestMethod]
    public void ExplorerOptInCanBeChangedAndPolicyStillOverridesIt()
    {
        var store = new MemoryStore();
        bool? policy = null;
        var settings = new ConsumerSettings(store, (key, name) =>
            key == PowerScriptsPolicies.ConsumersKey && name == "file-explorer" ? policy : null, () => true, () => false);
        settings.Set("file-explorer", true);
        Assert.IsTrue(settings.Get("file-explorer").IsEnabled);
        policy = false;
        Assert.IsFalse(settings.Get("file-explorer").IsEnabled);
        Assert.ThrowsException<InvalidOperationException>(() => settings.Set("file-explorer", true));
        policy = null;
        Assert.IsTrue(settings.Get("file-explorer").IsEnabled);
    }

    [TestMethod]
    public void SavingMigratesLegacyChoiceAndPreservesUnrelatedConfiguration()
    {
        var store = new MemoryStore();
        store.WriteBlob("config.json", """{"enabled":true,"mxc":{"enabled":true},"custom":"keep"}""");
        var settings = new ConsumerSettings(store, (_, _) => null, () => true, () => true);
        settings.Set("keyboard-manager", false);
        var config = JsonNode.Parse(store.ReadBlob("config.json")!)!;
        Assert.IsTrue(config["consumers"]!["advanced-paste"]!.GetValue<bool>());
        Assert.IsFalse(config["consumers"]!["keyboard-manager"]!.GetValue<bool>());
        Assert.IsTrue(config["consumers"]!["command-palette"]!.GetValue<bool>());
        Assert.IsTrue(config["mxc"]!["enabled"]!.GetValue<bool>());
        Assert.AreEqual("keep", config["custom"]!.GetValue<string>());
        settings = new ConsumerSettings(store, (_, _) => null, () => true, () => false);
        Assert.IsTrue(settings.Get("advanced-paste").UserEnabled, "Central settings must win over the legacy field.");
    }

    [TestMethod]
    public void SavingCommandPalettePreservesExistingConsumersAndUnrelatedConfiguration()
    {
        var store = new MemoryStore();
        store.WriteBlob("config.json", """{"consumers":{"advanced-paste":false,"keyboard-manager":false,"file-explorer":true,"future-consumer":false},"mxc":{"enabled":true}}""");
        var settings = new ConsumerSettings(store, (_, _) => null, () => true, () => true);
        settings.Set("command-palette", false);

        var config = JsonNode.Parse(store.ReadBlob("config.json")!)!;
        Assert.IsFalse(config["consumers"]!["advanced-paste"]!.GetValue<bool>());
        Assert.IsFalse(config["consumers"]!["keyboard-manager"]!.GetValue<bool>());
        Assert.IsTrue(config["consumers"]!["file-explorer"]!.GetValue<bool>());
        Assert.IsFalse(config["consumers"]!["future-consumer"]!.GetValue<bool>());
        Assert.IsFalse(config["consumers"]!["command-palette"]!.GetValue<bool>());
        Assert.IsTrue(config["mxc"]!["enabled"]!.GetValue<bool>());

        settings.Set("keyboard-manager", true);
        Assert.IsFalse(settings.Get("command-palette").UserEnabled, "Changing another integration must preserve the Command Palette choice.");
        settings.Set("command-palette", true);
        Assert.IsTrue(settings.Get("command-palette").IsEnabled);
        Assert.IsFalse(settings.Get("advanced-paste").IsEnabled);
    }

    [TestMethod]
    public void SnapshotIncludesCommandPaletteWhenExistingConfigurationContainsOnlyTwoConsumers()
    {
        var store = new MemoryStore();
        const string configuration = """{"consumers":{"advanced-paste":true,"keyboard-manager":false},"custom":"keep"}""";
        store.WriteBlob("config.json", configuration);
        var settings = new ConsumerSettings(store, (_, _) => null, () => true, () => false);
        var snapshot = settings.GetSnapshot();

        CollectionAssert.AreEquivalent(
            new[] { "advanced-paste", "keyboard-manager", "command-palette", "file-explorer" },
            snapshot.Consumers.Select(item => item.Id).ToArray());
        Assert.IsTrue(snapshot.Consumers.Single(item => item.Id == "command-palette").IsEnabled);
        Assert.IsTrue(snapshot.Consumers.Single(item => item.Id == "advanced-paste").IsEnabled);
        Assert.IsFalse(snapshot.Consumers.Single(item => item.Id == "keyboard-manager").IsEnabled);
        Assert.IsFalse(snapshot.Consumers.Single(item => item.Id == "file-explorer").IsEnabled);
        Assert.AreEqual(configuration, store.ReadBlob("config.json"), "Reading an old configuration must not rewrite it.");
    }

    [TestMethod]
    public void CommandPaletteHonorsCmdPalParentPolicyAndRestoresSavedPreference()
    {
        var store = new MemoryStore();
        const string configuration = """{"consumers":{"command-palette":true}}""";
        store.WriteBlob("config.json", configuration);
        bool? parentPolicy = false;
        var reads = new List<string>();
        bool? Read(string key, string name)
        {
            reads.Add(name);
            return key == PowerScriptsPolicies.PolicyKey && name == "ConfigureEnabledUtilityCmdPal" ? parentPolicy : null;
        }

        var settings = new ConsumerSettings(store, Read, () => true, () => false);
        var state = settings.Get("command-palette");
        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.CanChange);
        Assert.IsTrue(state.IsPolicyControlled);
        Assert.AreEqual("parent-policy-disabled", state.DisabledReason);
        CollectionAssert.Contains(reads, "ConfigureEnabledUtilityCmdPal");
        CollectionAssert.DoesNotContain(reads, "ConfigureEnabledUtilityCommandPalette");
        Assert.ThrowsException<InvalidOperationException>(() => settings.Set("command-palette", true));

        parentPolicy = null;
        Assert.IsTrue(settings.Get("command-palette").IsEnabled);
        Assert.IsTrue(settings.Get("command-palette").CanChange);
        Assert.AreEqual(configuration, store.ReadBlob("config.json"));
    }

    [TestMethod]
    public void PolicyRemovalRestoresPreferenceWithoutWritingIt()
    {
        var store = new MemoryStore();
        store.WriteBlob("config.json", """{"consumers":{"advanced-paste":false}}""");
        var before = store.ReadBlob("config.json");
        bool? policy = true;
        var settings = new ConsumerSettings(store, (key, _) => key == PowerScriptsPolicies.ConsumersKey ? policy : null, () => true, () => false);
        Assert.IsTrue(settings.Get("advanced-paste").IsEnabled);
        Assert.IsFalse(settings.Get("advanced-paste").CanChange);
        Assert.ThrowsException<InvalidOperationException>(() => settings.Set("advanced-paste", false));
        policy = null;
        Assert.IsFalse(settings.Get("advanced-paste").IsEnabled);
        Assert.IsTrue(settings.Get("advanced-paste").CanChange);
        Assert.AreEqual(before, store.ReadBlob("config.json"));
    }

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    [DataRow("""{"value":true}""", true)]
    [DataRow("""{"value":false}""", false)]
    public void FileBackedMigrationReadsOnlySiblingAdvancedPasteSettings(string value, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "powerscripts-consumer-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "AdvancedPaste"));
            File.WriteAllText(Path.Combine(root, "AdvancedPaste", "settings.json"), new JsonObject
            {
                ["properties"] = new JsonObject { ["powerscripts-enabled"] = JsonNode.Parse(value) },
            }.ToJsonString());
            var store = new FileSettingsStore(Path.Combine(root, "PowerScripts"));
            var settings = new ConsumerSettings(store, (_, _) => null, () => true);
            Assert.AreEqual(expected, settings.Get("advanced-paste").UserEnabled);
            settings.Set("keyboard-manager", false);
            File.WriteAllText(Path.Combine(root, "AdvancedPaste", "settings.json"), """{"properties":{"powerscripts-enabled":false}}""");
            Assert.AreEqual(expected, settings.Get("advanced-paste").UserEnabled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void InvalidConfigurationIsReportedAndNeverReplaced()
    {
        foreach (var text in new[] { "{", "[]", "null", """{"consumers":[]}""", """{"consumers":{"advanced-paste":"yes"}}""" })
        {
            var store = new MemoryStore();
            store.WriteBlob("config.json", text);
            var settings = new ConsumerSettings(store, (_, _) => null, () => true, () => false);
            Exception? failure = null;
            try
            {
                settings.Get("advanced-paste");
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            Assert.IsNotNull(failure, text);
            Assert.AreEqual(text, store.ReadBlob("config.json"));
        }
    }

    [TestMethod]
    public void MachinePolicyWinsWithoutReadingUserPolicyAndInvalidValuesFailClosed()
    {
        foreach (var value in new object[] { 0, 1, 2, "1", 1L })
        {
            object? Read(bool machine)
            {
                Assert.IsTrue(machine, "Configured machine policy must never consult HKCU.");
                return value;
            }

            if (value is int number && number is 0 or 1)
            {
                Assert.AreEqual(number == 1, PowerScriptsPolicies.ResolveValue(Read));
            }
            else
            {
                Assert.ThrowsException<InvalidDataException>(() => PowerScriptsPolicies.ResolveValue(Read));
            }
        }

        Assert.AreEqual(false, PowerScriptsPolicies.ResolveValue(machine => machine ? null : 0));
        Assert.AreEqual(true, PowerScriptsPolicies.ResolveValue(machine => machine ? null : 1));
        Assert.IsNull(PowerScriptsPolicies.ResolveValue(_ => null));
        Assert.ThrowsException<UnauthorizedAccessException>(() =>
            PowerScriptsPolicies.ResolveValue(_ => throw new UnauthorizedAccessException("Policy unavailable")));
    }

    [TestMethod]
    public void IndividualUtilityPolicyOverridesAllUtilityDefault()
    {
        bool? individual = true;
        bool? Read(string _, string name) => name == "ConfigureEnabledUtilityPowerScripts" ? individual : false;
        Assert.AreEqual(true, PowerScriptsPolicies.ReadUtility("PowerScripts", Read));
        individual = null;
        Assert.AreEqual(false, PowerScriptsPolicies.ReadUtility("PowerScripts", Read));
    }

    private sealed class MemoryStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _blobs = new();

        public string? ReadBlob(string key) => _blobs.GetValueOrDefault(key);

        public void WriteBlob(string key, string contents) => _blobs[key] = contents;
    }
}
