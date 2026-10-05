// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconPresentationStateTests
{
    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void IntermediateIsSelectedOnlyWhenItIsActuallyPresented(bool hasResolvedSource, bool hasPlacementFallback, bool isPresented)
    {
        var state = new IconPresentationState<object>();
        if (hasResolvedSource)
        {
            state.SetResolvedSource(new object(), expectsImageSource: true);
        }

        if (hasPlacementFallback)
        {
            state.PlacementFallback = new object();
        }

        var intermediate = new object();
        state.SetRequestFallback(intermediate);

        var selected = state.SelectSource(preferFallbackForResolvedSource: false);
        Assert.AreEqual(isPresented, ReferenceEquals(selected, intermediate));
    }

    [TestMethod]
    public void RetainedSourceSurvivesPendingReplacements()
    {
        var state = new IconPresentationState<string> { PlacementFallback = "placement" };
        state.SetRequestFallback("old request");
        state.SetResolvedSource("frame 1", expectsImageSource: true);

        state.BeginSourceChange(retainResolvedSource: true);
        Assert.IsNull(state.RequestFallback);
        Assert.IsTrue(state.HasResolvedSource);
        Assert.IsTrue(state.ResolvedSourceExpectsImage);
        Assert.AreEqual("frame 1", state.SelectSource(preferFallbackForResolvedSource: false));

        state.SetRequestFallback("intermediate");
        state.BeginSourceChange(retainResolvedSource: true);
        Assert.AreEqual("frame 1", state.SelectSource(preferFallbackForResolvedSource: false));
        Assert.IsNull(state.RequestFallback);

        state.SetResolvedSource("frame 2", expectsImageSource: true);
        Assert.AreEqual("frame 2", state.SelectSource(preferFallbackForResolvedSource: false));
    }

    [TestMethod]
    public void FailedReplacementUsesFallbackWithoutRestoringPreviousImage()
    {
        var state = new IconPresentationState<string> { PlacementFallback = "placement" };
        state.SetResolvedSource("frame 1", expectsImageSource: true);
        state.BeginSourceChange(retainResolvedSource: true);
        state.SetResolvedSource(null, expectsImageSource: false);

        Assert.AreEqual("placement", state.SelectSource(preferFallbackForResolvedSource: true));
        state.BeginSourceChange(retainResolvedSource: true);
        Assert.AreEqual("placement", state.SelectSource(preferFallbackForResolvedSource: true));
    }

    [TestMethod]
    public void SourceChangeResetsResolvedSourceToPlacementFallback()
    {
        var state = new IconPresentationState<string>
        {
            PlacementFallback = "placement",
        };
        state.SetResolvedSource("preceding item", expectsImageSource: true);

        Assert.IsTrue(state.ResolvedSourceExpectsImage);

        state.BeginSourceChange();

        Assert.AreEqual("placement", state.SelectSource(preferFallbackForResolvedSource: false));
        Assert.IsFalse(state.HasResolvedSource);
        Assert.IsFalse(state.ResolvedSourceExpectsImage);
    }

    [TestMethod]
    public void PlacementFallbackTakesPrecedenceOverRequestFallback()
    {
        var state = new IconPresentationState<string>
        {
            PlacementFallback = "placement",
        };
        state.SetRequestFallback("request");

        Assert.AreEqual("placement", state.SelectSource(preferFallbackForResolvedSource: false));
    }

    [TestMethod]
    public void ResolvedSourceReplacesFallbackUnlessPlacementPrefersFallback()
    {
        var state = new IconPresentationState<string>
        {
            PlacementFallback = "fallback",
        };
        state.SetResolvedSource("resolved", expectsImageSource: false);

        Assert.AreEqual("resolved", state.SelectSource(preferFallbackForResolvedSource: false));
        Assert.AreEqual("fallback", state.SelectSource(preferFallbackForResolvedSource: true));
    }

    [TestMethod]
    public void NewSourceDoesNotRetainPreviousRequestFallback()
    {
        var state = new IconPresentationState<string>();
        state.SetRequestFallback("preceding request");

        state.BeginSourceChange();

        Assert.IsNull(state.RequestFallback);
        Assert.IsNull(state.SelectSource(preferFallbackForResolvedSource: false));
    }

    [TestMethod]
    public async Task AsyncInitialsUsesPlacementFallbackUntilResolution()
    {
        const string Value = "|Initials|Å|info|circle|";
        var state = new IconPresentationState<string>
        {
            PlacementFallback = "placement",
        };
        state.SetResolvedSource("recycled", expectsImageSource: true);

        state.BeginSourceChange();
        Assert.IsFalse(GeneratedIconProtocolProcessor.Instance.TryPrepareSynchronously(
            Value,
            20,
            ElementTheme.Light,
            out var synchronousIcon));
        Assert.IsNull(synchronousIcon);
        Assert.AreEqual("placement", state.SelectSource(preferFallbackForResolvedSource: false));

        using var result = await GeneratedIconProtocolProcessor.Instance.PrepareAsync(
            Value,
            20,
            ElementTheme.Light);
        using var preparedIcon = result.TakePreparedIcon();
        Assert.IsNotNull(preparedIcon);
        state.SetResolvedSource("initials", expectsImageSource: true);

        Assert.AreEqual("initials", state.SelectSource(preferFallbackForResolvedSource: false));
    }
}
