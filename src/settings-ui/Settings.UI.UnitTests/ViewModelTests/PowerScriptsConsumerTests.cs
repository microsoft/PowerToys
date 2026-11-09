// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        CollectionAssert.AreEquivalent(new[] { nameof(consumer.IsEnabled), nameof(consumer.CanChange), nameof(consumer.Status) }, changed);

        consumer.Update(new("command-palette", true, true, true, false, string.Empty));

        Assert.IsTrue(consumer.IsEnabled, "Removing policy must expose the saved integration preference.");
        Assert.IsTrue(consumer.CanChange);
    }

    [DataTestMethod]
    [DataRow("advanced-paste", "PowerScripts_ActivityAdvancedPaste")]
    [DataRow("keyboard-manager", "PowerScripts_ActivityKeyboardManager")]
    [DataRow("command-palette", "PowerScripts_ActivityCommandPalette")]
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
