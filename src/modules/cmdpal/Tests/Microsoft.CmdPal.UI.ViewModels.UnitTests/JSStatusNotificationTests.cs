// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Exercises the host/showStatus notification path end to end against an in-memory
/// fake extension, verifying that the host reads the SDK status wire shape correctly:
/// the indeterminate progress payload and the Pascal-case State severity nested in
/// the message object.
/// </summary>
[TestClass]
public partial class JSStatusNotificationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ShowStatus_IndeterminateProgress_SetsProgressStateOnStatusMessage()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitializedProvider(fake, out var host);

        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["Message"] = "Working",
                    ["State"] = 0,
                },
                ["progress"] = new JsonObject { ["isIndeterminate"] = true },
                ["context"] = "extension",
            });

        var status = await host.WaitForStatusAsync();

        Assert.AreEqual("Working", status.Message);
        Assert.IsNotNull(status.Progress);
        Assert.IsTrue(status.Progress!.IsIndeterminate);

        GC.KeepAlive(provider);
    }

    [TestMethod]
    public async Task ShowStatus_DeterminateProgress_MapsProgressPercent()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitializedProvider(fake, out var host);

        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["Message"] = "Half done",
                    ["State"] = 0,
                },
                ["progress"] = new JsonObject
                {
                    ["isIndeterminate"] = false,
                    ["progressPercent"] = 50,
                },
                ["context"] = "extension",
            });

        var status = await host.WaitForStatusAsync();

        Assert.IsNotNull(status.Progress);
        Assert.IsFalse(status.Progress!.IsIndeterminate);
        Assert.AreEqual(50u, status.Progress.ProgressPercent);

        GC.KeepAlive(provider);
    }

    [DataTestMethod]
    [DataRow(0, MessageState.Info)]
    [DataRow(1, MessageState.Success)]
    [DataRow(2, MessageState.Warning)]
    [DataRow(3, MessageState.Error)]
    public async Task ShowStatus_PascalCaseState_MapsToSeverity(int stateValue, MessageState expected)
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitializedProvider(fake, out var host);

        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["Message"] = $"State {stateValue}",
                    ["State"] = stateValue,
                },
                ["context"] = "extension",
            });

        var status = await host.WaitForStatusAsync();

        Assert.AreEqual(expected, status.State);
        Assert.IsNull(status.Progress);

        GC.KeepAlive(provider);
    }

    private static JSCommandProviderProxy CreateInitializedProvider(JSFakeExtension fake, out CapturingExtensionHost host)
    {
        host = new CapturingExtensionHost();
        var provider = new JSCommandProviderProxy(
            fake.Connection,
            new JSExtensionManifest { Name = "test.ext", DisplayName = "Test Extension" });
        provider.InitializeWithHost(host);
        return provider;
    }

    private sealed partial class CapturingExtensionHost : IExtensionHost
    {
        private readonly TaskCompletionSource<IStatusMessage> _statusShown =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IStatusMessage> WaitForStatusAsync() => _statusShown.Task.WaitAsync(Timeout);

        public IAsyncAction ShowStatus(IStatusMessage? message, StatusContext context)
        {
            if (message is not null)
            {
                _statusShown.TrySetResult(message);
            }

            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction HideStatus(IStatusMessage? message) => Task.CompletedTask.AsAsyncAction();

        public IAsyncAction LogMessage(ILogMessage? message) => Task.CompletedTask.AsAsyncAction();
    }
}
