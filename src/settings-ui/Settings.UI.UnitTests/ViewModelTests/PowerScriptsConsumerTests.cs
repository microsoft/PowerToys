// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.UI.ViewModels;

namespace ViewModelTests;

[TestClass]
public class PowerScriptsConsumerTests
{
    [TestMethod]
    public void CommandPaletteConsumerWaitsForAuthoritativeHostSettings()
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem("command-palette");

        Assert.AreEqual("command-palette", consumer.Id);
        Assert.IsFalse(consumer.IsEnabled);
        Assert.IsFalse(consumer.CanChange);
    }

    [DataTestMethod]
    [DataRow(true, false, false, false, "module-disabled", "PowerScripts_ConsumerModuleDisabled")]
    [DataRow(true, false, false, true, "parent-policy-disabled", "PowerScripts_ConsumerParentPolicyDisabled")]
    [DataRow(true, false, false, true, "", "PowerScripts_ConsumerManaged")]
    [DataRow(false, true, false, true, "", "PowerScripts_ConsumerManaged")]
    [DataRow(false, false, true, false, "", "PowerScripts_ConsumerDescription")]
    [DataRow(true, true, true, false, "", "PowerScripts_ConsumerDescription")]
    public void CommandPaletteConsumerDisplaysEffectiveAvailability(
        bool userEnabled,
        bool isEnabled,
        bool canChange,
        bool policyControlled,
        string disabledReason,
        string statusResource)
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem("command-palette");
        consumer.Update(new("command-palette", userEnabled, isEnabled, canChange, policyControlled, disabledReason));

        Assert.AreEqual(isEnabled, consumer.IsEnabled);
        Assert.AreEqual(canChange, consumer.CanChange);
        Assert.AreEqual(PowerScriptsViewModel.GetResourceString(statusResource), consumer.Status);
    }

    [TestMethod]
    public void CommandPaletteConsumerNotifiesBindingsWhenPolicyChanges()
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem("command-palette");
        consumer.Update(new("command-palette", true, true, true, false, string.Empty));
        var changed = new List<string>();
        consumer.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        consumer.Update(new("command-palette", true, false, false, true, "parent-policy-disabled"));

        Assert.IsFalse(consumer.IsEnabled);
        Assert.IsFalse(consumer.CanChange);
        CollectionAssert.IsSubsetOf(
            new[] { nameof(consumer.IsEnabled), nameof(consumer.CanChange), nameof(consumer.Status), nameof(consumer.Description), nameof(consumer.CatalogueStatus) },
            changed);

        consumer.Update(new("command-palette", true, true, true, false, string.Empty));

        Assert.IsTrue(consumer.IsEnabled, "Removing policy must expose the saved integration preference.");
        Assert.IsTrue(consumer.CanChange);
    }

    [DataTestMethod]
    [DataRow("advanced-paste", "text-text,files-text,image-text,audio-text,text-files,text-image,text-audio")]
    [DataRow("keyboard-manager", "none-none,none-text,none-files,none-image,none-audio")]
    [DataRow("command-palette", "none-none,none-text")]
    [DataRow("file-explorer", "files-text,image-text,audio-text")]
    public void ModulesListOnlyCompatibleScriptContracts(string consumerId, string expectedIds)
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem(consumerId);
        consumer.Update(new(consumerId, true, true, true, false, string.Empty));
        var contracts = new[]
        {
            "text-text", "files-text", "image-text", "audio-text",
            "text-files", "text-image", "text-audio", "text-none",
            "none-none", "none-text", "none-files", "none-image", "none-audio",
            "unknown-text", "none-unknown",
        };
        var scripts = contracts.Select(contract =>
        {
            var io = contract.Split('-');
            return new PowerScriptListItem
            {
                Id = contract,
                Name = contract,
                Io = new() { Input = io[0], Output = io[1] },
            };
        }).Append(new PowerScriptListItem { Id = "missing-io", Io = null });

        consumer.UpdateApplicableScripts(scripts);

        CollectionAssert.AreEquivalent(expectedIds.Split(','), consumer.ApplicableScripts.Select(script => script.Id).ToArray());
        Assert.IsFalse(consumer.HasCatalogueStatus);
    }

    [DataTestMethod]
    [DataRow("files")]
    [DataRow("image")]
    [DataRow("audio")]
    public void FileExplorerListsEverySupportedOutputForSelectedFileInputs(string input)
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem("file-explorer");
        consumer.Update(new(consumer.Id, true, true, true, false, string.Empty));
        var outputs = new[] { "none", "text", "files", "image", "audio" };
        consumer.UpdateApplicableScripts(outputs.Append("unknown").Select(output => new PowerScriptListItem
        {
            Id = output,
            Io = new() { Input = input, Output = output },
        }));

        CollectionAssert.AreEquivalent(outputs, consumer.ApplicableScripts.Select(script => script.Id).ToArray());
    }

    [TestMethod]
    public void ModulesSortScriptsByNameThenStableIdentity()
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem("command-palette");
        consumer.Update(new(consumer.Id, true, true, true, false, string.Empty));
        consumer.UpdateApplicableScripts(
        [
            new() { Id = "z", Name = "Alpha", Io = new() { Input = "NONE", Output = "TEXT" } },
            new() { Id = "b", Name = "Beta", Io = new() { Input = "none", Output = "none" } },
            new() { Id = "a", Name = "Alpha", Io = new() { Input = "none", Output = "text" } },
        ]);

        Assert.AreEqual("a,z,b", string.Join(",", consumer.ApplicableScripts.Select(script => script.Id)));
    }

    [DataTestMethod]
    [DataRow("keyboard-manager", "none", false, "")]
    [DataRow("keyboard-manager", "none", true, "module-disabled")]
    [DataRow("keyboard-manager", "none", true, "parent-policy-disabled")]
    [DataRow("file-explorer", "files", false, "")]
    [DataRow("file-explorer", "files", true, "module-disabled")]
    public void DisablingIntegrationClearsScriptsAndCollapsesTheModule(string consumerId, string input, bool userEnabled, string reason)
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem(consumerId);
        consumer.Update(new(consumer.Id, true, true, true, false, string.Empty));
        PowerScriptListItem[] scripts = [new() { Id = "action", Io = new() { Input = input, Output = "none" } }];
        consumer.UpdateApplicableScripts(scripts);
        consumer.IsExpanded = true;
        Assert.AreEqual(1, consumer.ApplicableScripts.Count);

        consumer.Update(new(consumer.Id, userEnabled, false, false, reason.Length > 0, reason));
        consumer.UpdateApplicableScripts(scripts);

        Assert.AreEqual(0, consumer.ApplicableScripts.Count);
        Assert.IsFalse(consumer.IsExpanded);
        Assert.IsTrue(consumer.HasCatalogueStatus);
        Assert.AreEqual(PowerScriptsViewModel.GetResourceString("PowerScripts_ModuleScriptsEnable"), consumer.CatalogueStatus);
    }

    [TestMethod]
    public void EnabledModuleWithNoCompatibleScriptsShowsEmptyState()
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem("command-palette");
        consumer.Update(new(consumer.Id, true, true, true, false, string.Empty));
        consumer.UpdateApplicableScripts([new() { Io = new() { Input = "text", Output = "text" } }]);

        Assert.IsTrue(consumer.HasCatalogueStatus);
        Assert.AreEqual(PowerScriptsViewModel.GetResourceString("PowerScripts_ModuleScriptsEmpty"), consumer.CatalogueStatus);
    }

    [DataTestMethod]
    [DataRow("command-palette", "none")]
    [DataRow("file-explorer", "files")]
    public void RefreshingModuleCatalogueReplacesTheSnapshotWithoutMutatingNavigatedViews(string consumerId, string input)
    {
        var consumer = new PowerScriptsViewModel.PowerScriptConsumerItem(consumerId);
        consumer.Update(new(consumer.Id, true, true, true, false, string.Empty));
        consumer.UpdateApplicableScripts([new() { Id = "first", Io = new() { Input = input, Output = "none" } }]);
        var previous = consumer.ApplicableScripts;

        consumer.UpdateApplicableScripts([new() { Id = "second", Io = new() { Input = input, Output = "text" } }]);

        Assert.AreEqual("first", previous.Single().Id);
        Assert.AreEqual("second", consumer.ApplicableScripts.Single().Id);
        consumer.Update(new(consumer.Id, false, false, true, false, string.Empty));
        Assert.AreEqual("first", previous.Single().Id);
        Assert.AreEqual(0, consumer.ApplicableScripts.Count);
    }

    [DataTestMethod]
    [DataRow("advanced-paste", "PowerScripts_ActivityAdvancedPaste")]
    [DataRow("keyboard-manager", "PowerScripts_ActivityKeyboardManager")]
    [DataRow("command-palette", "PowerScripts_ActivityCommandPalette")]
    [DataRow("file-explorer", "PowerScripts_ActivityFileExplorer")]
    [DataRow("command-line", "PowerScripts_ActivityCommandLine")]
    public void ScriptActivityUsesLocalizedConsumerName(string consumerId, string resource)
    {
        var activity = CreateActivity(consumerId);

        Assert.AreEqual(PowerScriptsViewModel.GetResourceString(resource), activity.Source);
        StringAssert.Contains(activity.Details, activity.Source);
    }

    [TestMethod]
    public void ScriptActivityPreservesUnknownConsumerName()
    {
        Assert.AreEqual("future-consumer", CreateActivity("future-consumer").Source);
    }

    private static PowerScriptActivityItem CreateActivity(string consumerId) => new()
    {
        Id = Guid.Empty,
        ScriptId = "example-script",
        ScriptName = "Example script",
        ConsumerId = consumerId,
        StartedUtc = DateTimeOffset.UnixEpoch,
        Status = "succeeded",
    };
}
