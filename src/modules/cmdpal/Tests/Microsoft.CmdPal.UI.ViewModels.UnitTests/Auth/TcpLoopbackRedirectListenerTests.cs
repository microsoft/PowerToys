// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests.Auth;

[TestClass]
public sealed class TcpLoopbackRedirectListenerTests
{
    [TestMethod]
    public void RedirectUri_BindsLoopbackOnly()
    {
        using var listener = new TcpLoopbackRedirectListener();
        StringAssert.StartsWith(listener.RedirectUri, "http://127.0.0.1:");
        StringAssert.EndsWith(listener.RedirectUri, "/");
    }

    [TestMethod]
    public async Task WaitForRedirect_CapturesCallbackQuery_AndIgnoresOtherPaths()
    {
        using var listener = new TcpLoopbackRedirectListener();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var waitTask = listener.WaitForRedirectAsync(cts.Token);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // A non-callback path (e.g. favicon) must be answered and ignored so the
        // listener keeps waiting for the real redirect.
        try
        {
            await client.GetAsync(listener.RedirectUri + "favicon.ico", cts.Token);
        }
        catch (HttpRequestException)
        {
        }

        Assert.IsFalse(waitTask.IsCompleted, "listener should still be waiting after a non-callback request");

        // The real redirect on the callback path ("/").
        var response = await client.GetAsync(listener.RedirectUri + "?code=abc123&state=xyz789", cts.Token);
        Assert.IsTrue(response.IsSuccessStatusCode);

        IReadOnlyDictionary<string, string> captured = await waitTask;
        Assert.AreEqual("abc123", captured["code"]);
        Assert.AreEqual("xyz789", captured["state"]);
    }
}
