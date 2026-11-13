// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptsServiceTests
{
    private static readonly string[] ExpectedSortedScriptIds = ["a", "z", "beta"];

    [TestMethod]
    [DataRow(PowerScriptIO.None, PowerScriptIO.None, true)]
    [DataRow(PowerScriptIO.None, PowerScriptIO.Text, true)]
    [DataRow(PowerScriptIO.None, PowerScriptIO.Files, false)]
    [DataRow(PowerScriptIO.None, PowerScriptIO.Image, false)]
    [DataRow(PowerScriptIO.None, PowerScriptIO.Audio, false)]
    [DataRow(PowerScriptIO.Text, PowerScriptIO.Text, false)]
    [DataRow(PowerScriptIO.Files, PowerScriptIO.None, false)]
    [DataRow(PowerScriptIO.Image, PowerScriptIO.Text, false)]
    [DataRow(PowerScriptIO.Audio, PowerScriptIO.None, false)]
    public void CompatibilityRequiresNoPrimaryInputAndSupportedOutput(PowerScriptIO input, PowerScriptIO output, bool expected)
    {
        var script = new PowerScriptInfo { Id = "contract.test", Input = input, Output = output };

        Assert.AreEqual(expected, PowerScriptsService.IsCompatible(script));
    }

    [TestMethod]
    public void UnsupportedScriptIsRejectedBeforeCreatingAHostClient()
    {
        var clientsCreated = 0;
        var service = new PowerScriptsService(() =>
        {
            clientsCreated++;
            throw new InvalidOperationException("The host client must not be created for an incompatible script.");
        });
        var script = new PowerScriptInfo { Id = "needs.files", Input = PowerScriptIO.Files };

        Assert.ThrowsExactly<ArgumentException>(() => service.RunAsync(script, new Dictionary<string, JsonElement>(), CancellationToken.None));
        Assert.AreEqual(0, clientsCreated);
    }

    [TestMethod]
    public async Task DiscoveryRetainsItsOwnedClientAndDisposalPreventsRestart()
    {
        var created = 0;
        using var service = new PowerScriptsService(() =>
        {
            created++;
            return new PowerScriptsClient(
                PowerScriptConsumer.CommandPalette,
                reuseHost: true,
                hostPath: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-host.exe"));
        });

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => service.DiscoverAsync(CancellationToken.None));
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => service.DiscoverAsync(CancellationToken.None));
        Assert.AreEqual(1, created);
        service.Dispose();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => service.DiscoverAsync(CancellationToken.None));
        Assert.AreEqual(1, created);
    }

    [TestMethod]
    [DataRow(true, true, false, "integration-disabled", false)]
    [DataRow(false, false, true, "module-disabled", false)]
    [DataRow(false, true, false, "module-disabled", true)]
    [DataRow(true, false, true, "policy-disabled", true)]
    public void DisabledCatalogAttributesTheReasonToTheSettingThatActuallyBlocksExecution(
        bool moduleEnabled,
        bool modulePolicyControlled,
        bool consumerPolicyControlled,
        string expectedReason,
        bool expectedPolicyControlled)
    {
        var state = new PowerScriptConsumerState
        {
            Id = PowerScriptConsumer.CommandPalette,
            IsPowerScriptsEnabled = moduleEnabled,
            IsPowerScriptsPolicyControlled = modulePolicyControlled,
            IsEnabled = false,
            IsPolicyControlled = consumerPolicyControlled,
        };

        var catalog = PowerScriptsService.CreateCatalog(state, [TestScripts.Create()]);

        Assert.IsFalse(catalog.IsEnabled);
        Assert.HasCount(0, catalog.Scripts);
        Assert.AreEqual(expectedReason, catalog.DisabledReason);
        Assert.AreEqual(expectedPolicyControlled, catalog.IsPolicyControlled);
    }

    [TestMethod]
    public void CatalogPreservesTheHostsDisabledParentReason()
    {
        var state = new PowerScriptConsumerState
        {
            IsPowerScriptsEnabled = true,
            IsEnabled = false,
            IsPolicyControlled = true,
            DisabledReason = "parent-policy-disabled",
        };

        var catalog = PowerScriptsService.CreateCatalog(state, [TestScripts.Create()]);

        Assert.AreEqual("parent-policy-disabled", catalog.DisabledReason);
        Assert.IsTrue(catalog.IsPolicyControlled);
        Assert.IsFalse(catalog.IsEnabled);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnabledCatalogFiltersUnsupportedContractsAndSortsByNameThenStableId(bool consumerPolicyControlled)
    {
        var state = new PowerScriptConsumerState
        {
            IsPowerScriptsEnabled = true,
            IsPowerScriptsPolicyControlled = true,
            IsEnabled = true,
            IsPolicyControlled = consumerPolicyControlled,
        };
        PowerScriptInfo[] scripts =
        [
            new() { Id = "z", Name = "Alpha", Input = PowerScriptIO.None, Output = PowerScriptIO.None },
            new() { Id = "beta", Name = "Beta", Input = PowerScriptIO.None, Output = PowerScriptIO.Text },
            new() { Id = "a", Name = "alpha", Input = PowerScriptIO.None, Output = PowerScriptIO.Text },
            new() { Id = "input", Name = "Needs text input", Input = PowerScriptIO.Text, Output = PowerScriptIO.Text },
            new() { Id = "output", Name = "Returns files", Input = PowerScriptIO.None, Output = PowerScriptIO.Files },
        ];

        var catalog = PowerScriptsService.CreateCatalog(state, scripts);

        Assert.IsTrue(catalog.IsEnabled);
        Assert.IsNull(catalog.DisabledReason);
        Assert.AreEqual(consumerPolicyControlled, catalog.IsPolicyControlled);
        CollectionAssert.AreEqual(ExpectedSortedScriptIds, catalog.Scripts.Select(script => script.Id).ToArray());
    }

    [TestMethod]
    public void ProviderPublishesBrowserAndResultsFallbackWithoutDiscoveringOrRunningScripts()
    {
        var service = new FakePowerScriptsService
        {
            Catalog = new([TestScripts.Create()], true),
        };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });

        var commands = provider.TopLevelCommands();

        Assert.HasCount(1, commands);
        Assert.AreEqual("PowerScripts", commands[0].Title);
        Assert.AreEqual(0, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
        Assert.AreSame(commands, provider.TopLevelCommands());
        var fallback = provider.FallbackCommands().Single();
        Assert.IsInstanceOfType<PowerScriptsListPage>(commands[0].Command);
        Assert.IsInstanceOfType<FallbackResultsItem>(fallback);
        Assert.AreEqual(PowerScriptsFallback.FallbackId, ((IFallbackCommandItem2)fallback).Id);
        Assert.AreSame(commands[0].Command, fallback.Command);
        Assert.IsNotNull(((IFallbackCommandItem3)fallback).QueryHandler);
    }
}
