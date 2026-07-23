// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies host status lifecycle handling on the proxy (p4-06): a repeated status id
/// updates the existing message in place rather than stacking a duplicate, a status is
/// removed by id independent of its severity, and all active statuses are cleared when
/// the extension disconnects even without an explicit hide.
/// </summary>
[TestClass]
public class JSCommandProviderProxyStatusLifecycleTests
{
    private static JSCommandProviderProxy CreateInitialized(JSFakeExtension fake, out RecordingExtensionHost host)
    {
        host = new RecordingExtensionHost();
        var provider = new JSCommandProviderProxy(
            fake.Connection,
            new JSExtensionManifest { Name = "status.ext", DisplayName = "Status Extension" });
        provider.InitializeWithHost(host);
        return provider;
    }

    private static JsonObject ShowStatus(string statusId, string message, int state) => new()
    {
        ["statusId"] = statusId,
        ["message"] = new JsonObject { ["Message"] = message, ["State"] = state },
    };

    [TestMethod]
    public async Task RepeatedStatusId_UpdatesInPlace_WithoutDuplicateShow()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitialized(fake, out var host);

        await fake.PushNotificationAsync("host/showStatus", ShowStatus("s1", "First", 0));
        await host.WaitForShownCountAsync(1);

        // Update the same status, then push a second distinct status as an ordering
        // barrier: once the second status is shown, the in-place update has been applied.
        await fake.PushNotificationAsync("host/showStatus", ShowStatus("s1", "Second", 0));
        await fake.PushNotificationAsync("host/showStatus", ShowStatus("s2", "Other", 0));
        await host.WaitForShownCountAsync(2);

        Assert.AreEqual(2, host.Shown.Count, "The repeated status id must not produce a second ShowStatus.");
        Assert.AreEqual("Second", host.Shown[0].Message, "The existing status must be updated in place.");

        provider.Dispose();
    }

    [TestMethod]
    public async Task Status_RemovedById_IndependentOfSeverity()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitialized(fake, out var host);

        await fake.PushNotificationAsync("host/showStatus", ShowStatus("w1", "Careful", 2));
        await host.WaitForShownCountAsync(1);
        Assert.AreEqual(MessageState.Warning, host.Shown[0].State);

        await fake.PushNotificationAsync("host/hideStatus", new JsonObject { ["statusId"] = "w1" });
        await host.WaitForHiddenCountAsync(1);

        Assert.AreEqual(1, host.Hidden.Count);
        Assert.AreSame(host.Shown[0], host.Hidden[0], "The hidden status must be the warning shown by that id.");

        provider.Dispose();
    }

    [TestMethod]
    public async Task ActiveStatuses_AreCleared_OnDisconnect()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitialized(fake, out var host);

        await fake.PushNotificationAsync("host/showStatus", ShowStatus("s1", "Live", 0));
        await host.WaitForShownCountAsync(1);

        // Disconnecting the extension (its process exiting) must clear active statuses
        // even though no explicit hide arrived.
        fake.Dispose();

        await host.WaitForHiddenCountAsync(1);
        Assert.AreSame(host.Shown[0], host.Hidden[0]);

        provider.Dispose();
    }
}
