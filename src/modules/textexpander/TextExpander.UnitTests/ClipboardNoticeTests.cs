// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>What the notice at the caret says when a clipboard variable's spot was left empty.</summary>
[TestClass]
public sealed class ClipboardNoticeTests
{
    [TestMethod]
    [DataRow(ClipboardPart.Email, ClipboardGapReason.NotFound, "No email address found", "There's no email address in what you copied, so that spot was left empty.")]
    [DataRow(ClipboardPart.Link, ClipboardGapReason.NotFound, "No link found", "There's no link in what you copied, so that spot was left empty.")]
    [DataRow(ClipboardPart.Phone, ClipboardGapReason.NotFound, "No phone number found", "There's no phone number in what you copied, so that spot was left empty.")]
    [DataRow(ClipboardPart.Number, ClipboardGapReason.NotFound, "No number found", "There's no number in what you copied, so that spot was left empty.")]
    [DataRow(ClipboardPart.All, ClipboardGapReason.NotFound, "No text copied", "There's no text on your clipboard, so that spot was left empty.")]
    [DataRow(ClipboardPart.Email, ClipboardGapReason.HistoryOff, "Clipboard history is off", "Clipboard history is off, so that spot was left empty.")]
    [DataRow(ClipboardPart.All, ClipboardGapReason.NoCopies, "No copies in clipboard history", "Your clipboard history has no text copies, so that spot was left empty.")]
    [DataRow(ClipboardPart.Link, ClipboardGapReason.HistoryUnreadable, "Couldn't read clipboard history", "Text Expander couldn't read your clipboard history in time, so that spot was left empty.")]
    public void OneEmptySpotSaysWhatWasntThereAndThatItsSpotIsEmpty(ClipboardPart part, ClipboardGapReason reason, string heading, string message)
        => Assert.AreEqual((heading, message), ClipboardNotice.For([new ClipboardGap(part, reason)]));

    [TestMethod]
    public void SpotsLeftEmptyForTheSameReasonAreToldOnce()
    {
        Assert.AreEqual(
            ("No email address found", "There's no email address in what you copied, so those spots were left empty."),
            ClipboardNotice.For([new ClipboardGap(ClipboardPart.Email, ClipboardGapReason.NotFound), new ClipboardGap(ClipboardPart.Email, ClipboardGapReason.NotFound)]));

        // Clipboard history that's off is off for any part.
        Assert.AreEqual(
            ("Clipboard history is off", "Clipboard history is off, so those spots were left empty."),
            ClipboardNotice.For([new ClipboardGap(ClipboardPart.Email, ClipboardGapReason.HistoryOff), new ClipboardGap(ClipboardPart.All, ClipboardGapReason.HistoryOff)]));
    }

    [TestMethod]
    public void DifferentReasonsAreEachALineInTheSnippetsOrder()
    {
        (string heading, string message) = ClipboardNotice.For(
        [
            new ClipboardGap(ClipboardPart.Phone, ClipboardGapReason.NotFound),
            new ClipboardGap(ClipboardPart.Email, ClipboardGapReason.NotFound),
            new ClipboardGap(ClipboardPart.Phone, ClipboardGapReason.NotFound),
            new ClipboardGap(ClipboardPart.Link, ClipboardGapReason.HistoryOff),
        ])!.Value;

        Assert.AreEqual("Some spots were left empty", heading);
        Assert.AreEqual(
            "There's no phone number in what you copied, so those spots were left empty.\n"
            + "There's no email address in what you copied, so that spot was left empty.\n"
            + "Clipboard history is off, so that spot was left empty.",
            message);
    }

    [TestMethod]
    public void NothingLeftEmptyMeansNoNotice()
    {
        Assert.IsNull(ClipboardNotice.For([]));
        ClipboardNotice.Show(IntPtr.Zero, []);
    }

    [TestMethod]
    public void EveryPartAndReasonHasItsWords()
    {
        foreach (ClipboardPart part in Enum.GetValues<ClipboardPart>())
        {
            foreach (ClipboardGapReason reason in Enum.GetValues<ClipboardGapReason>())
            {
                foreach (int spots in new[] { 1, 2 })
                {
                    (string heading, string message) = ClipboardNotice.For(Enumerable.Repeat(new ClipboardGap(part, reason), spots).ToList())!.Value;
                    Assert.IsTrue(heading.Length > 0 && !heading.EndsWith('.'), $"{part} {reason}: {heading}");
                    Assert.IsTrue(message.EndsWith('.') && !message.Contains('\n', StringComparison.Ordinal), $"{part} {reason}: {message}");
                    StringAssert.Contains(message, spots == 1 ? "that spot was left empty" : "those spots were left empty");
                }
            }
        }
    }
}
