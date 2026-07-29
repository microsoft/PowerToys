// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies that status hiding cannot race disposal and strand UI (r2-p4-08). A status
/// show is only ever delivered to the host while the proxy is not disposed, and disposal
/// hides every active status under the same lock, so once teardown has hidden statuses no
/// later show can resurrect them. The guarantee under test is the invariant "every status
/// the host was shown is also hidden": teardown can drop a show that had not yet been
/// delivered, but it can never leave a delivered show without a matching hide.
/// </summary>
[TestClass]
public class JSCommandProviderProxyStatusDisposeRaceTests
{
    private static JSCommandProviderProxy CreateInitialized(JSFakeExtension fake, out RecordingExtensionHost host)
    {
        host = new RecordingExtensionHost();
        var provider = new JSCommandProviderProxy(
            fake.Connection,
            new JSExtensionManifest { Name = "status.race.ext", DisplayName = "Status Race Extension" });
        provider.InitializeWithHost(host);
        return provider;
    }

    private static JsonObject ShowStatus(string statusId, string message) => new()
    {
        ["statusId"] = statusId,
        ["message"] = new JsonObject { ["Message"] = message, ["State"] = 0 },
    };

    [TestMethod]
    public async Task Dispose_HidesActiveStatuses()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateInitialized(fake, out var host);

        await fake.PushNotificationAsync("host/showStatus", ShowStatus("s1", "Live"));
        await host.WaitForShownCountAsync(1);

        provider.Dispose();

        // A status that was visible at teardown must be hidden exactly once, with the same
        // message instance, so nothing is left stranded in the host UI.
        Assert.AreEqual(1, host.Hidden.Count);
        Assert.AreSame(host.Shown[0], host.Hidden[0]);
    }

    [TestMethod]
    public async Task ConcurrentShowAndDispose_NeverStrandsStatus()
    {
        const int Iterations = 50;

        for (var i = 0; i < Iterations; i++)
        {
            using var fake = new JSFakeExtension();
            var provider = CreateInitialized(fake, out var host);

            // Push a status and tear the proxy down without waiting for the show to be
            // delivered, so the show handler races Dispose. Whichever wins the lock, the
            // show is either delivered-then-hidden or its late delivery is dropped: it must
            // never be recorded as shown without a matching hide.
            var pushTask = fake.PushNotificationAsync("host/showStatus", ShowStatus($"s{i}", "Racing"));
            provider.Dispose();
            await pushTask;

            // Give the connection's delivery thread a chance to run the (now no-op) handler
            // so the assertion sees the settled state rather than an in-flight one.
            await Task.Delay(5);

            var shown = host.Shown;
            var hidden = host.Hidden;

            // The invariant: every status the host was actually shown must also have been
            // hidden (matched by reference). Before the fix, a show delivered after teardown
            // hid statuses would have no matching hide and would strand the UI.
            foreach (var message in shown)
            {
                Assert.IsTrue(
                    hidden.Any(h => ReferenceEquals(h, message)),
                    "A status shown to the host must always be hidden; disposal must not strand it.");
            }

            provider.Dispose();
        }
    }
}
