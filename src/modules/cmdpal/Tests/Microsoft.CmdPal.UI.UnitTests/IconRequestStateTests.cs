// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconRequestStateTests
{
    [TestMethod]
    public void SameOwnerRetainsCandidatesUntilANewerResultIsApplied()
    {
        var state = new IconRequestState();
        var owner = new object();
        state.ChangeSource(new object(), ElementTheme.Light, owner, out _);
        var first = state.Begin();
        state.ChangeSource(new object(), ElementTheme.Light, owner, out var retain);
        var second = state.Begin();

        Assert.IsTrue(retain);
        Assert.IsTrue(state.CanApply(first, hasResult: true));
        Assert.IsFalse(state.CanApply(first, hasResult: false));
        Assert.IsFalse(state.ShouldClearOnFailure(first));
        state.MarkApplied(second);
        Assert.IsFalse(state.CanApply(first, hasResult: true));
        Assert.IsFalse(state.ShouldClearOnFailure(second));
    }

    [TestMethod]
    public void OwnersUseReferenceIdentityAndCannotReviveOldRequests()
    {
        var state = new IconRequestState();
        var owner = new Owner(1);
        state.ChangeSource(new object(), ElementTheme.Light, owner, out _);
        var first = state.Begin();
        state.ChangeSource(new object(), ElementTheme.Light, new Owner(1), out var retain);
        var recycled = state.Begin();
        Assert.IsFalse(retain);
        state.ChangeSource(new object(), ElementTheme.Light, owner, out retain);

        Assert.IsFalse(retain);
        Assert.IsFalse(state.CanApply(first, hasResult: true));
        Assert.IsFalse(state.CanApply(recycled, hasResult: true));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ClearOrUnownedReplacementInvalidatesEveryCandidate(bool clear)
    {
        var state = new IconRequestState();
        var owner = new object();
        state.ChangeSource(new object(), ElementTheme.Light, owner, out _);
        var first = state.Begin();
        state.ChangeSource(new object(), ElementTheme.Light, owner, out _);
        var second = state.Begin();

        state.ChangeSource(clear ? null : new object(), ElementTheme.Light, null, out var retain);

        Assert.IsFalse(retain);
        Assert.IsNull(state.PresentationOwner);
        Assert.IsFalse(state.IsLatestRequestActive);
        Assert.IsFalse(state.CanApply(first, hasResult: true));
        Assert.IsFalse(state.CanApply(second, hasResult: true));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ThemeChangeRetainsPresentationButInvalidatesCandidates(bool owned)
    {
        var state = new IconRequestState();
        var key = new object();
        var owner = owned ? new object() : null;
        state.ChangeSource(key, ElementTheme.Light, owner, out _);
        var request = state.Begin();

        var sourceChanged = state.ChangeSource(key, ElementTheme.Dark, owner, out var retain);

        Assert.IsFalse(sourceChanged);
        Assert.IsTrue(retain);
        Assert.AreEqual(ElementTheme.Dark, state.Theme);
        Assert.IsFalse(state.CanApply(request, hasResult: true));
    }

    [TestMethod]
    public void ThemeWithNoOwnerResetsOwnershipForTheNextSourceChange()
    {
        var state = new IconRequestState();
        var key = new object();
        var owner = new object();
        state.ChangeSource(key, ElementTheme.Light, owner, out _);
        state.ChangeSource(key, ElementTheme.Dark, null, out _);
        state.ChangeSource(new object(), ElementTheme.Dark, owner, out var retain);

        Assert.IsFalse(retain);
    }

    [TestMethod]
    public void InvalidatedRequestsCannotCompleteAReplacementRequest()
    {
        var state = new IconRequestState();
        var first = state.Begin();
        Assert.IsTrue(state.IsLatestRequestActive);
        state.Invalidate();
        Assert.IsFalse(state.IsLatestRequestActive);
        var second = state.Begin();
        state.Complete(first);

        Assert.IsTrue(state.IsCurrent(second));
        Assert.IsFalse(state.CanApply(first, hasResult: true));
        state.Complete(second);
        Assert.IsFalse(state.IsLatestRequestActive);
    }

    [TestMethod]
    public void AppliedFailurePreventsAnOlderFrameFromReturning()
    {
        var state = new IconRequestState();
        var owner = new object();
        state.ChangeSource(new object(), ElementTheme.Light, owner, out _);
        var first = state.Begin();
        state.ChangeSource(new object(), ElementTheme.Light, owner, out _);
        var second = state.Begin();
        Assert.IsTrue(state.ShouldClearOnFailure(second));
        state.MarkApplied(second);
        state.Complete(second);

        Assert.IsFalse(state.CanApply(first, hasResult: true));
        Assert.IsFalse(state.ShouldClearOnFailure(second));
        Assert.IsTrue(state.ShouldClearOnFailure(state.Begin()));
    }

    private sealed record Owner(int Value);
}
