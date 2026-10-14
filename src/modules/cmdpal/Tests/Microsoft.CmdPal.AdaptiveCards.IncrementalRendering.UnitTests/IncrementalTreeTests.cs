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

        var plan = IncrementalTreeDiffer.CreatePlan(tree, tree);

        Assert.AreEqual(IncrementalPlanDisposition.NoChanges, plan.Disposition);
        Assert.AreEqual(0, plan.PropertyUpdates.Count);
    }

    [TestMethod]
    public void TextChangeProducesValidatedPatch()
    {
        var plan = IncrementalTreeDiffer.CreatePlan(TextTree("old"), TextTree("new"));

        Assert.AreEqual(IncrementalPlanDisposition.PatchInPlace, plan.Disposition);
        Assert.AreEqual(1, plan.PropertyUpdates.Count);
        var update = plan.PropertyUpdates[0];
        Assert.AreEqual(1, update.NodeIndex);
        Assert.AreEqual("TextBlock", update.ExpectedNodeType);
        Assert.AreEqual("old", update.ExpectedOldValue);
        Assert.AreEqual("new", update.NewValue);
    }

    [TestMethod]
    public void InlineSvgChangeProducesValidatedPatch()
    {
        var plan = IncrementalTreeDiffer.CreatePlan(
            ImageTree("data:image/svg+xml;utf8,<svg id='old'/>"),
            ImageTree("data:image/svg+xml;utf8,<svg id='new'/>"));

        Assert.AreEqual(IncrementalPlanDisposition.PatchInPlace, plan.Disposition);
        Assert.AreEqual(1, plan.PropertyUpdates.Count);
        var update = plan.PropertyUpdates[0];
        Assert.AreEqual(1, update.NodeIndex);
        Assert.AreEqual("ImageSource", update.PropertyName);
        Assert.AreEqual("data:image/svg+xml;utf8,<svg id='old'/>", update.ExpectedOldValue);
        Assert.AreEqual("data:image/svg+xml;utf8,<svg id='new'/>", update.NewValue);
    }

    [TestMethod]
    public void StructuralChangeReplacesRoot()
    {
        var current = TextTree("hello");
        var candidate = Tree(new IncrementalNodeSnapshot("Root", 0));

        var plan = IncrementalTreeDiffer.CreatePlan(current, candidate);

        Assert.AreEqual(IncrementalPlanDisposition.ReplaceRoot, plan.Disposition);
    }

    [TestMethod]
    public void TreeShapeChangeReplacesRoot()
    {
        var current = Tree(
            new IncrementalNodeSnapshot("Root", 2),
            new IncrementalNodeSnapshot("Container", 1),
            new IncrementalNodeSnapshot("TextBlock", 0),
            new IncrementalNodeSnapshot("TextBlock", 0));
        var candidate = Tree(
            new IncrementalNodeSnapshot("Root", 2),
            new IncrementalNodeSnapshot("Container", 0),
            new IncrementalNodeSnapshot("TextBlock", 1),
            new IncrementalNodeSnapshot("TextBlock", 0));

        var plan = IncrementalTreeDiffer.CreatePlan(current, candidate);

        Assert.AreEqual(IncrementalPlanDisposition.ReplaceRoot, plan.Disposition);
    }

    [TestMethod]
    public void ReplacementPropertyChangeReplacesRoot()
    {
        var plan = IncrementalTreeDiffer.CreatePlan(
            RootWithFingerprint("one"),
            RootWithFingerprint("two"));

        Assert.AreEqual(IncrementalPlanDisposition.ReplaceRoot, plan.Disposition);
    }

    private static IncrementalTreeSnapshot TextTree(string text)
    {
        return Tree(
            new IncrementalNodeSnapshot("Root", 1),
            new IncrementalNodeSnapshot(
                "TextBlock",
                0,
                [
                    new(
                        "Text",
                        text,
                        IncrementalPropertyBehavior.PatchInPlace),
                ]));
    }

    private static IncrementalTreeSnapshot RootWithFingerprint(string fingerprint)
    {
        return Tree(
            new IncrementalNodeSnapshot(
                "Root",
                0,
                [
                    new(
                        "CardSemantics",
                        fingerprint,
                        IncrementalPropertyBehavior.ReplaceRoot),
                ]));
    }

    private static IncrementalTreeSnapshot ImageTree(string resource)
    {
        return Tree(
            new IncrementalNodeSnapshot("Root", 1),
            new IncrementalNodeSnapshot(
                "Image",
                0,
                [
                    new(
                        "ImageSource",
                        resource,
                        IncrementalPropertyBehavior.PatchInPlace),
                ]));
    }

    private static IncrementalTreeSnapshot Tree(params IncrementalNodeSnapshot[] nodes) => new(nodes);
}
