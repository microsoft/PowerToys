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
        foreach (var id in new[] { "advanced-paste", "keyboard-manager" })
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
                                var parent = id == "advanced-paste" ? "AdvancedPaste" : "KeyboardManager";
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
        Assert.IsTrue(config["mxc"]!["enabled"]!.GetValue<bool>());
        Assert.AreEqual("keep", config["custom"]!.GetValue<string>());
        settings = new ConsumerSettings(store, (_, _) => null, () => true, () => false);
        Assert.IsTrue(settings.Get("advanced-paste").UserEnabled, "Central settings must win over the legacy field.");
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
