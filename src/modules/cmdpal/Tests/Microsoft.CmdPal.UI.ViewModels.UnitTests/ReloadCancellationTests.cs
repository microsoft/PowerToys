// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies the replaceable reload cancellation source (p4-01). A single
/// CancellationTokenSource can only cancel once, so a service that shares one token
/// between stop and a later reload would keep handing out an already canceled token.
/// These tests confirm a fresh, uncanceled token is available after a stop.
/// </summary>
[TestClass]
public class ReloadCancellationTests
{
    [TestMethod]
    public void BeginCycle_AfterStop_YieldsFreshUncanceledToken()
    {
        using var reload = new ReloadCancellation();

        var first = reload.BeginCycle();
        Assert.IsFalse(first.IsCancellationRequested, "A new cycle should start uncanceled.");

        reload.Stop();
        Assert.IsTrue(reload.IsStopRequested, "A stop should be observable.");
        Assert.IsTrue(first.IsCancellationRequested, "In-flight token should observe the stop.");

        // The load-stop-load sequence: a second cycle must produce a live token so
        // providers load on the second load instead of silently doing nothing.
        var second = reload.BeginCycle();
        Assert.IsFalse(second.IsCancellationRequested, "The second load cycle should get a live token.");
        Assert.IsFalse(reload.IsStopRequested, "The wrapper should no longer report a stop after a new cycle.");
    }

    [TestMethod]
    public void Token_AfterDispose_IsCanceledAndDoesNotThrow()
    {
        var reload = new ReloadCancellation();
        reload.BeginCycle();
        reload.Dispose();

        Assert.IsTrue(reload.IsStopRequested);
        Assert.IsTrue(reload.Token.IsCancellationRequested);

        // Begin after dispose returns a canceled token rather than throwing.
        Assert.IsTrue(reload.BeginCycle().IsCancellationRequested);

        // Dispose is idempotent.
        reload.Dispose();
    }

    [TestMethod]
    public void Stop_WithoutBeginCycle_StillCancelsCurrentToken()
    {
        using var reload = new ReloadCancellation();

        var token = reload.Token;
        reload.Stop();

        Assert.IsTrue(token.IsCancellationRequested);
        Assert.IsTrue(reload.IsStopRequested);
    }
}
