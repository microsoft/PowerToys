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
    public void OkReturnsWhatWasTypedAndCancelReturnsNull()
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
        Assert.AreEqual("PowerToys Text Expander", PromptText.Title, "The prompt pops up over other apps, so it names PowerToys.");
    }

    [TestMethod]
    public void AChoiceAsksForItsPartAndShowsEachOptionOnOneLine()
    {
        Assert.AreEqual("Pick the link to insert", ClipboardChoiceText.Heading(new ClipboardChoice(ClipboardPart.Link, FromHistory: false, AskedInOrder, IsLast: true)));
        Assert.AreEqual("Pick the phone number to insert", ClipboardChoiceText.Heading(new ClipboardChoice(ClipboardPart.Phone, FromHistory: false, AskedInOrder, IsLast: true)));
        Assert.AreEqual("Pick a copy to insert", ClipboardChoiceText.Heading(new ClipboardChoice(ClipboardPart.All, FromHistory: true, AskedInOrder, IsLast: true)));
        Assert.AreEqual("Pick the email address to insert", ClipboardChoiceText.Heading(new ClipboardChoice(ClipboardPart.Email, FromHistory: true, AskedInOrder, IsLast: true)), "Matches across recent copies are asked for as matches.");

        Assert.AreEqual("Best regards, Your name", ClipboardChoiceText.OneLine("  Best regards,\r\n\tYour name\n"));
        Assert.AreEqual(string.Empty, ClipboardChoiceText.OneLine(null));
        string shown = ClipboardChoiceText.OneLine(new string('x', 500));
        Assert.AreEqual(ClipboardChoiceText.MaxShown + 1, shown.Length);
        Assert.IsTrue(shown.EndsWith('\u2026'), "More is in its tooltip and name.");
        Assert.AreEqual("full\ntext", new ClipboardChoiceOption("full\ntext").Text, "What's inserted is the whole text.");
    }

    [TestMethod]
    public void ALongCopyIsInsertedWholeButReadAndShownInPart()
    {
        var option = new ClipboardChoiceOption(new string('y', 50_000), 3);
        Assert.AreEqual(50_000, option.Text.Length, "What's inserted is the whole copy.");
        Assert.AreEqual("3", option.Number);
        Assert.AreEqual(ClipboardChoiceText.MaxSpoken + 1, option.Spoken.Length, "Screen readers hear its start, then that more follows.");
        Assert.AreEqual(ClipboardChoiceText.MaxTip + 1, option.Tip.Length);
        Assert.AreEqual("Short", new ClipboardChoiceOption("Short").Spoken, "A match is read whole.");
        Assert.AreEqual("abcd\u2026", ClipboardChoiceText.Capped("abcd\U0001F600", 5), "Not between the halves of a character.");
    }

    [TestMethod]
    public void FewerThanTwoOptionsNeedNoWindow()
    {
        // Core asks only with two or more, but the dialog stands on its own: one goes in and none inserts nothing, and keys
        // typed from then on still wait for the snippet.
        int accepted = 0;
        Assert.AreEqual(string.Empty, ClipboardChoiceDialog.Show(new ClipboardChoice(ClipboardPart.All, FromHistory: true, [], IsLast: true), IntPtr.Zero, () => accepted++));
        Assert.AreEqual("Call back on Friday", ClipboardChoiceDialog.Show(new ClipboardChoice(ClipboardPart.All, FromHistory: true, ["Call back on Friday"], IsLast: true), IntPtr.Zero, () => accepted++));
        Assert.AreEqual(2, accepted);
    }

    [TestMethod]
    public void ClipboardHistoryIsReadAheadOnlyForASnippetThatPicksFromItAndWaitedForAtMostAWhile()
    {
        Assert.IsTrue(ClipboardChoiceDialog.PicksCopy("{{clipboard:pick}}"));
        Assert.IsTrue(ClipboardChoiceDialog.PicksCopy("Hi {{prompt:Name}}, see {{clipboard:pick:link}}"));
        Assert.IsFalse(ClipboardChoiceDialog.PicksCopy("{{clipboard:link}}"), "That takes from what's on the clipboard now.");
        Assert.IsFalse(ClipboardChoiceDialog.PicksCopy("{{clipboard:link:pick}}"), "Not a variable: it stays as typed.");
        Assert.IsFalse(ClipboardChoiceDialog.PicksCopy("{{Clipboard:pick}}"), "Typed as written.");
        Assert.IsFalse(ClipboardChoiceDialog.PicksCopy("{{date:'{{clipboard:pick}}'}}"), "Part of a date format.");

        var read = new ClipboardHistory(ClipboardHistoryState.Read, ["you@example.com"]);
        Assert.AreSame(read, ClipboardChoiceDialog.Await(Task.FromResult(read)));
        Assert.AreSame(ClipboardHistory.TurnedOff, ClipboardChoiceDialog.Await(Task.FromResult(ClipboardHistory.TurnedOff)));
        var never = new TaskCompletionSource<ClipboardHistory>();
        Assert.AreEqual(ClipboardHistoryState.Unreadable, ClipboardChoiceDialog.Await(never.Task, TimeSpan.FromMilliseconds(50)).State, "Not read in time.");
        Assert.AreEqual(ClipboardHistoryState.Unreadable, ClipboardChoiceDialog.Await(Task.FromException<ClipboardHistory>(new InvalidOperationException())).State);
    }

    [TestMethod]
    public void AHistoryReadThatTimedOutIsNotStartedAgainBesideIt()
    {
        // Each snippet that picks from history would otherwise start another read next to one hung in the clipboard service.
        var hung = new TaskCompletionSource<ClipboardHistory>(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        Task<ClipboardHistory> Hung()
        {
            Interlocked.Increment(ref started);
            return hung.Task;
        }

        try
        {
            Assert.AreEqual(ClipboardHistoryState.Unreadable, ClipboardHistoryReader.Read(static () => true, Hung, TimeSpan.FromMilliseconds(50)).State);
            Assert.AreEqual(ClipboardHistoryState.Unreadable, ClipboardHistoryReader.Read(static () => true, Hung, TimeSpan.FromMilliseconds(50)).State);
            Assert.AreSame(ClipboardHistory.TurnedOff, ClipboardHistoryReader.Read(static () => false, Hung, TimeSpan.FromMilliseconds(50)));
            Assert.AreEqual(1, Volatile.Read(ref started));
        }
        finally
        {
            hung.SetResult(new ClipboardHistory(ClipboardHistoryState.Read, ["older copy"]));
        }

        // Once it ends, the next read starts afresh rather than taking what it read.
        var fresh = new ClipboardHistory(ClipboardHistoryState.Read, ["you@example.com"]);
        ClipboardHistory? read = null;
        Assert.IsTrue(SpinWait.SpinUntil(() => (read = ClipboardHistoryReader.Read(static () => true, () => Task.FromResult(fresh), TimeSpan.FromSeconds(5))).State == ClipboardHistoryState.Read, TimeSpan.FromSeconds(10)));
        Assert.AreSame(fresh, read);
        Assert.AreEqual(1, Volatile.Read(ref started));
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
    public void PromptsAreAskedOneAtATimeInOrder()
    {
        var asked = new List<string>();
        string text = Variables.Expand(
            "To {{prompt:Name}} from {{prompt:Team}}, re {{prompt:Name}}",
            label =>
            {
                asked.Add(label);
                return PromptText.Result(accepted: true, label == "Team" ? "Docs" : "Ada");
            });

        CollectionAssert.AreEqual(AskedInOrder, asked);
        Assert.AreEqual("To Ada from Docs, re Ada", text);
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

    [TestMethod]
    public void TheChoiceWindowGrowsWithTheTextSize()
    {
        // At 200% text, its 420 DIPs cut a long link short (F5). Its screen still caps it, as above.
        Assert.AreEqual(420, PromptWindowSize.ForTextScale(420, 1.0));
        Assert.AreEqual(840, PromptWindowSize.ForTextScale(420, 2.0));
        Assert.AreEqual(945, PromptWindowSize.ForTextScale(420, 2.25));
        Assert.AreEqual(420, PromptWindowSize.ForTextScale(420, 0.5), "Never narrower.");
        Assert.AreEqual(420, PromptWindowSize.ForTextScale(420, double.NaN));
    }
}
