// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering.UnitTests;

[TestClass]
public sealed class IncrementalTreeTests
{
    [TestMethod]
    public void IdenticalTreesProduceNoChanges()
    {
        var tree = TextTree("hello");

        var plan = IncrementalTreeDiffer.CreatePlan(tree, tree, 7);

        Assert.AreEqual(IncrementalPlanDisposition.NoChanges, plan.Disposition);
        Assert.AreEqual(7, plan.ExpectedVersion);
        Assert.AreEqual(0, plan.PropertyUpdates.Count);
    }

    [TestMethod]
    public void TextChangeProducesValidatedPatch()
    {
        var plan = IncrementalTreeDiffer.CreatePlan(TextTree("old"), TextTree("new"), 2);

        Assert.AreEqual(IncrementalPlanDisposition.PatchInPlace, plan.Disposition);
        Assert.AreEqual(1, plan.PropertyUpdates.Count);
        var update = plan.PropertyUpdates[0];
        Assert.AreEqual("$/0", update.NodePath);
        Assert.AreEqual("TextBlock", update.ExpectedNodeType);
        Assert.AreEqual("old", update.ExpectedOldValue.GetString());
        Assert.AreEqual("new", update.NewValue.GetString());
    }

    [TestMethod]
    public void InlineSvgChangeProducesValidatedPatch()
    {
        var plan = IncrementalTreeDiffer.CreatePlan(
            ImageTree("data:image/svg+xml;utf8,<svg id='old'/>"),
            ImageTree("data:image/svg+xml;utf8,<svg id='new'/>"),
            3);

        Assert.AreEqual(IncrementalPlanDisposition.PatchInPlace, plan.Disposition);
        Assert.AreEqual(1, plan.PropertyUpdates.Count);
        var update = plan.PropertyUpdates[0];
        Assert.AreEqual("$/0", update.NodePath);
        Assert.AreEqual("ImageSource", update.PropertyName);
        Assert.AreEqual("data:image/svg+xml;utf8,<svg id='old'/>", update.ExpectedOldValue.GetString());
        Assert.AreEqual("data:image/svg+xml;utf8,<svg id='new'/>", update.NewValue.GetString());
    }

    [TestMethod]
    public void StructuralChangeReplacesRoot()
    {
        var current = TextTree("hello");
        var candidate = new IncrementalNodeSnapshot("$", "Root", null);

        var plan = IncrementalTreeDiffer.CreatePlan(current, candidate, 1);

        Assert.AreEqual(IncrementalPlanDisposition.ReplaceRoot, plan.Disposition);
        StringAssert.Contains(plan.FallbackReason, "child count");
    }

    [TestMethod]
    public void ReplacementPropertyChangeReplacesRoot()
    {
        var current = RootWithFingerprint("one");
        var candidate = RootWithFingerprint("two");

        var plan = IncrementalTreeDiffer.CreatePlan(current, candidate, 1);

        Assert.AreEqual(IncrementalPlanDisposition.ReplaceRoot, plan.Disposition);
        StringAssert.Contains(plan.FallbackReason, "CardSemantics");
    }

    [TestMethod]
    public void DuplicateStableIdsReplaceRoot()
    {
        var children = new[]
        {
            new IncrementalNodeSnapshot("$/0", "TextBlock", "duplicate"),
            new IncrementalNodeSnapshot("$/1", "TextBlock", "duplicate"),
        };
        var tree = new IncrementalNodeSnapshot("$", "Root", null, children: children);

        var plan = IncrementalTreeDiffer.CreatePlan(tree, tree, 1);

        Assert.AreEqual(IncrementalPlanDisposition.ReplaceRoot, plan.Disposition);
        StringAssert.Contains(plan.FallbackReason, "duplicate stable IDs");
    }

    private static IncrementalNodeSnapshot TextTree(string text)
    {
        var textProperties = new[]
        {
            new IncrementalPropertySnapshot(
                "Text",
                IncrementalValue.FromString(text),
                IncrementalPropertyBehavior.PatchInPlace),
        };
        var children = new[]
        {
            new IncrementalNodeSnapshot("$/0", "TextBlock", null, textProperties),
        };
        return new IncrementalNodeSnapshot("$", "Root", null, children: children);
    }

    private static IncrementalNodeSnapshot RootWithFingerprint(string fingerprint)
    {
        var properties = new[]
        {
            new IncrementalPropertySnapshot(
                "CardSemantics",
                IncrementalValue.FromString(fingerprint),
                IncrementalPropertyBehavior.ReplaceRoot),
        };
        return new IncrementalNodeSnapshot("$", "Root", null, properties);
    }

    private static IncrementalNodeSnapshot ImageTree(string resource)
    {
        var imageProperties = new[]
        {
            new IncrementalPropertySnapshot(
                "ImageSource",
                IncrementalValue.FromString(resource),
                IncrementalPropertyBehavior.PatchInPlace),
        };
        var children = new[]
        {
            new IncrementalNodeSnapshot("$/0", "Image", null, imageProperties),
        };
        return new IncrementalNodeSnapshot("$", "Root", null, children: children);
    }
}
