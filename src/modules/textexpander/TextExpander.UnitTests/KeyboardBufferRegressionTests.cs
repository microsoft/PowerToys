// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class KeyboardBufferRegressionTests
{
    [TestMethod]
    [DataRow("\U0001F600", false, true)]
    [DataRow("\U0001F600", true, true)]
    [DataRow(" \U0001F600", false, true)]
    [DataRow("x\U0001F600", false, false)]
    [DataRow("x", false, true)]
    [DataRow("xy", false, false)]
    [DataRow("e\u0301", false, false)]
    public void BackspaceRemovesARecordedSurrogatePairWithoutRemovingItsPrefix(string before, bool shift, bool expands)
    {
        // The accepted packet input records both UTF-16 units. Leaving the high surrogate after Backspace made the
        // next word-start trigger look as though it was still inside a word. Other text-element rules are unchanged.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("btw=By the way"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store)
        {
            CaptureForeground = static () => KeyboardHook.ForegroundCapture.Authorized,
            InPasswordBox = static () => false,
            UserInput = static () => { },
        };

        bool wordMode = WordBoundary.Required;
        try
        {
            WordBoundary.Required = true;
            foreach (char ch in before)
            {
                Assert.IsFalse(hook.OnKeyEvent(FakeTextBox.UserKey(ch, up: false) with { Flags = 0x10 }, Native.WM_KEYDOWN));
            }

            Assert.IsFalse(hook.ProcessKey(Native.VK_BACK, 0, ctrl: false, alt: false, shift: shift, win: false));
            bool matched = false;
            foreach (char ch in "btw ")
            {
                matched = hook.ProcessKey(Native.VK_PACKET, ch, ctrl: false, alt: false, shift: false, win: false);
            }

            Assert.AreEqual(expands, matched);
            Assert.AreEqual(expands ? 1 : 0, hook.Pending.Count);
            if (expands)
            {
                Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
                Assert.AreEqual("btw", work.TypedPrefix, "The pair is gone; only the new trigger may be erased.");
            }
        }
        finally
        {
            WordBoundary.Required = wordMode;
        }
    }
}
