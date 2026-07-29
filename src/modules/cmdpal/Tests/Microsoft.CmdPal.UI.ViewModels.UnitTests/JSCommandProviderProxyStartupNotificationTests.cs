// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies that host notifications an extension emits while it activates, before the
/// host is attached, are buffered and delivered once the host attaches (p4-05). The
/// proxy is constructed first so its notification handlers are registered in time to
/// receive those startup notifications.
/// </summary>
[TestClass]
public class JSCommandProviderProxyStartupNotificationTests
{
    private static JSCommandProviderProxy CreateProvider(JSFakeExtension fake) =>
        new(fake.Connection, new JSExtensionManifest { Name = "startup.ext", DisplayName = "Startup Extension" });

    [TestMethod]
    public async Task StartupStatus_EmittedBeforeHostAttaches_IsDeliveredAfterAttach()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);

        // The extension raises a status during activation, before the host is attached.
        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["statusId"] = "startup-1",
                ["message"] = new JsonObject { ["Message"] = "Starting", ["State"] = 0 },
            });

        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        await host.WaitForShownCountAsync(1);
        Assert.AreEqual(1, host.Shown.Count);
        Assert.AreEqual("Starting", host.Shown[0].Message);

        provider.Dispose();
    }

    [TestMethod]
    public async Task StartupLog_EmittedBeforeHostAttaches_IsDeliveredAfterAttach()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);

        await fake.PushNotificationAsync(
            "host/logMessage",
            new JsonObject { ["message"] = "activating", ["state"] = 0 });

        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        await host.WaitForLogCountAsync(1);
        Assert.AreEqual("activating", host.Logs[0].Message);

        provider.Dispose();
    }

    [TestMethod]
    public async Task BufferedActions_AreDeliveredInArrivalOrder()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);

        await fake.PushNotificationAsync(
            "host/logMessage",
            new JsonObject { ["message"] = "first", ["state"] = 0 });
        await fake.PushNotificationAsync(
            "host/logMessage",
            new JsonObject { ["message"] = "second", ["state"] = 0 });

        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        await host.WaitForLogCountAsync(2);
        Assert.AreEqual("first", host.Logs[0].Message);
        Assert.AreEqual("second", host.Logs[1].Message);

        provider.Dispose();
    }
}
