// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconRefreshStateTests
{
    [TestMethod]
    public void SourceThenHandlerCoalescesReasonsUntilDispatch()
    {
        var state = default(IconRefreshState);
        state.Request(hasSource: true, reason: IconRequestReason.SourceChanged);

        Assert.IsFalse(state.TryConsume(
            isLoaded: true,
            hasSource: true,
            hasHandler: false,
            out _));

        state.Request(hasSource: true, reason: IconRequestReason.HandlerAttached);

        Assert.IsTrue(state.TryConsume(
            isLoaded: true,
            hasSource: true,
            hasHandler: true,
            out var reason));
        Assert.AreEqual(
            IconRequestReason.SourceChanged | IconRequestReason.HandlerAttached,
            reason);
    }

    [TestMethod]
    public void HandlerThenSourceDoesNotRetainAReasonWithoutWork()
    {
        var state = default(IconRefreshState);
        state.Request(hasSource: false, reason: IconRequestReason.HandlerAttached);
        state.Request(hasSource: true, reason: IconRequestReason.SourceChanged);

        Assert.IsTrue(state.TryConsume(
            isLoaded: true,
            hasSource: true,
            hasHandler: true,
            out var reason));
        Assert.AreEqual(IconRequestReason.SourceChanged, reason);
    }

    [TestMethod]
    public void PendingRequestSurvivesUntilEveryRequirementIsAvailable()
    {
        var state = default(IconRefreshState);
        state.Request(hasSource: true, reason: IconRequestReason.Loaded);

        Assert.IsFalse(state.TryConsume(isLoaded: false, hasSource: true, hasHandler: true, out _));
        Assert.IsFalse(state.TryConsume(isLoaded: true, hasSource: false, hasHandler: true, out _));
        Assert.IsFalse(state.TryConsume(isLoaded: true, hasSource: true, hasHandler: false, out _));
        Assert.IsTrue(state.TryConsume(isLoaded: true, hasSource: true, hasHandler: true, out var reason));
        Assert.AreEqual(IconRequestReason.Loaded, reason);
        Assert.IsFalse(state.TryConsume(isLoaded: true, hasSource: true, hasHandler: true, out _));
    }

    [TestMethod]
    public void ClearingSourceDiscardsPendingReasons()
    {
        var state = default(IconRefreshState);
        state.Request(hasSource: true, reason: IconRequestReason.SourceChanged | IconRequestReason.ThemeChanged);
        state.Request(hasSource: false, reason: IconRequestReason.Retry);

        Assert.IsFalse(state.TryConsume(
            isLoaded: true,
            hasSource: true,
            hasHandler: true,
            out var reason));
        Assert.AreEqual(IconRequestReason.None, reason);
    }

    [TestMethod]
    public void RetryWaitsForAndCombinesWithAnExternalTrigger()
    {
        var state = default(IconRefreshState);
        state.Request(hasSource: true, reason: IconRequestReason.Retry);

        Assert.IsFalse(state.TryConsume(
            isLoaded: false,
            hasSource: true,
            hasHandler: true,
            out _));

        state.Request(hasSource: true, reason: IconRequestReason.Loaded);

        Assert.IsTrue(state.TryConsume(
            isLoaded: true,
            hasSource: true,
            hasHandler: true,
            out var reason));
        Assert.AreEqual(IconRequestReason.Retry | IconRequestReason.Loaded, reason);
    }
}
