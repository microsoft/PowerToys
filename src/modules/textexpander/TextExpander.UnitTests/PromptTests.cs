// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>The {{prompt:Label}} window's behavior that doesn't need a window.</summary>
[TestClass]
public sealed class PromptTests
{
    private static readonly string[] AskedInOrder = ["Name", "Team", "Name"];

    [TestMethod]
    public void OkReturnsWhatWasTypedAndCancelLeavesTheSpotBlank()
    {
        Assert.AreEqual("Ada", PromptText.Result(accepted: true, "Ada"));
        Assert.AreEqual(@"C:\temp", PromptText.Result(accepted: true, @"C:\temp"), "Returned as typed; Variables inserts it literally.");
        Assert.AreEqual(string.Empty, PromptText.Result(accepted: true, string.Empty), "OK with nothing typed inserts nothing.");
        Assert.AreEqual(string.Empty, PromptText.Result(accepted: true, null));
        Assert.IsNull(PromptText.Result(accepted: false, "typed, then cancelled"), "Cancel, Esc and the close button return null.");
    }

    [TestMethod]
    public void TheBoxIsEmptiedOnceTheAnswerIsReadEvenIfReadingFails()
    {
        string box = "private answer";
        Assert.AreEqual("private answer", PromptText.Take(() => box, () => box = string.Empty));
        Assert.AreEqual(string.Empty, box);

        box = "private answer";
        Assert.ThrowsException<InvalidOperationException>(() => PromptText.Take(() => throw new InvalidOperationException(), () => box = string.Empty));
        Assert.AreEqual(string.Empty, box, "Cleared even when reading failed.");

        Assert.AreEqual(string.Empty, PromptText.Take(() => null, () => { }));
    }

    [TestMethod]
    public void TheQuestionLabelsTheBox()
    {
        Assert.AreEqual("First name", PromptText.Label("First name"));
        Assert.AreEqual("what type", PromptText.Label("  what type "));
        Assert.AreEqual("Your answer", PromptText.Label(string.Empty), "{{prompt:}} has no label of its own.");
        Assert.AreEqual("Your answer", PromptText.Label(null));
        Assert.AreEqual("Text Expander", PromptText.Title);
    }

    [TestMethod]
    public void RequiredPromptsRejectBlankAnswersAndOptionalPromptsAllowThem()
    {
        var required = new PromptRequest("Name", Required: true);
        var optional = new PromptRequest("Note", Required: false);

        Assert.IsFalse(PromptText.HasRequiredAnswer(required, string.Empty));
        Assert.IsFalse(PromptText.HasRequiredAnswer(required, "   "));
        Assert.IsTrue(PromptText.HasRequiredAnswer(required, "Ada"));
        Assert.IsTrue(PromptText.HasRequiredAnswer(optional, string.Empty));
        Assert.AreEqual("Enter an answer to continue.", PromptText.RequiredError);
    }

    [TestMethod]
    public void PromptsAreAskedOneAtATimeInOrderAndACancelBlanksOnlyItsSpot()
    {
        var asked = new List<string>();
        string text = Variables.Expand(
            "To {{prompt:Name}} from {{prompt:Team}}, re {{prompt:Name}}",
            label =>
            {
                asked.Add(label);
                return PromptText.Result(accepted: label != "Team", "Ada");
            });

        CollectionAssert.AreEqual(AskedInOrder, asked);
        Assert.AreEqual("To Ada from , re Ada", text);
    }

    [TestMethod]
    public void TheWindowIsExactlyAsTallAsItsContentAtEveryScale()
    {
        // Measured at 125%: a one-line question lays out at 209.6 DIPs, title bar row included, and
        // the frame (a 1-pixel border plus Windows' invisible resize border) adds 9 pixels of height.
        // Only the content is scaled; the frame is pixels already.
        Assert.AreEqual(271, PromptWindowSize.WindowPixels(windowPixels: 269, clientPixels: 260, contentDips: 209.6, scale: 1.25));

        // At 100% and 150% the frame differs, and layout rounds to whole pixels at each scale.
        Assert.AreEqual(218, PromptWindowSize.WindowPixels(windowPixels: 260, clientPixels: 252, contentDips: 210, scale: 1.0));
        Assert.AreEqual(325, PromptWindowSize.WindowPixels(windowPixels: 380, clientPixels: 369, contentDips: 314 / 1.5, scale: 1.5));

        // A question that wraps onto a second line: measured at 228 DIPs.
        Assert.AreEqual(294, PromptWindowSize.WindowPixels(windowPixels: 269, clientPixels: 260, contentDips: 228, scale: 1.25));

        // Width: 420 DIPs of content, and 18 pixels of frame.
        Assert.AreEqual(543, PromptWindowSize.WindowPixels(windowPixels: 543, clientPixels: 525, contentDips: 420, scale: 1.25));
    }

    [TestMethod]
    public void TheSystemCaptionIsNotAddedBelowTheButtons()
    {
        // How the window opened before, at 125%: AppWindow.ResizeClient left room for a system caption
        // that isn't drawn, so 262 pixels of content got a 300-pixel client area, with the extra 38
        // below the buttons. Measuring the frame instead of assuming it takes that back off.
        Assert.AreEqual(271, PromptWindowSize.WindowPixels(windowPixels: 309, clientPixels: 300, contentDips: 209.6, scale: 1.25));

        // Already the right size: nothing changes, so the window doesn't move once it's loaded.
        Assert.AreEqual(271, PromptWindowSize.WindowPixels(windowPixels: 271, clientPixels: 262, contentDips: 209.6, scale: 1.25));
    }

    [TestMethod]
    public void ContentIsRoundedUpToWholePixelsAndCappedAtTheScreen()
    {
        Assert.AreEqual(525, PromptWindowSize.ContentPixels(420, 1.25));
        Assert.AreEqual(262, PromptWindowSize.ContentPixels(209.6, 1.25));
        Assert.AreEqual(262, PromptWindowSize.ContentPixels(209.60000000001, 1.25), "Floating-point noise doesn't add a pixel.");
        Assert.AreEqual(263, PromptWindowSize.ContentPixels(209.9, 1.25), "A part pixel is rounded up so nothing is cut off.");

        // A very long question never makes the window taller than the screen's work area.
        Assert.AreEqual(1140, PromptWindowSize.WindowPixels(windowPixels: 269, clientPixels: 260, contentDips: 2000, scale: 1.25, maxWindowPixels: 1140));
    }
}
